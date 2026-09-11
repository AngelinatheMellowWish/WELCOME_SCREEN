using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Object1688.Shared.Text;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 大字队列服务（架构 §3.2：多触发/多规则命中按序入队串行播放；F-07 提前结束）。
/// 消费泵运行于 UI 线程（WPF 窗口/动画/帧率监控均需 UI 上下文）；
/// 生产者可跨线程调用 <see cref="Enqueue"/> / <see cref="EndCurrent"/>（内部经 Dispatcher 封送）。
/// </summary>
public sealed class BannerQueueService
{
    private readonly Channel<BannerRequest> _queue = Channel.CreateUnbounded<BannerRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _logWarn;
    private readonly Func<string, double, double> _measureText;
    private readonly Action<bool>? _onPlayingChanged;
    private readonly Action<double, bool>? _onFpsSample;
    private readonly Action<double, double>? _onRenderCost;

    private BannerAnimation? _currentAnimation;
    private BannerSoundPlayer? _currentSound;
    private int _generation;

    /// <param name="dispatcher">UI 线程 Dispatcher（窗口创建所在线程）。</param>
    /// <param name="logWarn">警告日志回调（OVL-W-3004/OVL-W-3007 等）。</param>
    /// <param name="measureText">文本测量委托：返回 DIP 宽（WPF 侧 FormattedText 实现）。</param>
    /// <param name="onPlayingChanged">大字显示状态变化回调（true=开始显示 / false=结束），供 F-07 快捷键裁决。</param>
    /// <param name="onFpsSample">每秒帧率采样回调（fps, 降级态），供 AC-68 帧率曲线上报。</param>
    /// <param name="onRenderCost">触发瞬间渲染开销回调（CPU 毫秒, 内存 MB 增量），供 AC-95 展示/告警。</param>
    public BannerQueueService(
        Dispatcher dispatcher,
        Action<string> logWarn,
        Func<string, double, double> measureText,
        Action<bool>? onPlayingChanged = null,
        Action<double, bool>? onFpsSample = null,
        Action<double, double>? onRenderCost = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logWarn = logWarn ?? throw new ArgumentNullException(nameof(logWarn));
        _measureText = measureText ?? throw new ArgumentNullException(nameof(measureText));
        _onPlayingChanged = onPlayingChanged;
        _onFpsSample = onFpsSample;
        _onRenderCost = onRenderCost;
    }

    /// <summary>入队（线程安全；IPC 触发线程调用）。</summary>
    public void Enqueue(BannerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _queue.Writer.TryWrite(request);
    }

    /// <summary>
    /// 提前结束当前大字（F-07，线程安全）：浮现/保持阶段立即切入淡出并出队，后续队列继续。
    /// </summary>
    public void EndCurrent()
    {
        if (_dispatcher.CheckAccess())
        {
            EndCurrentCore();
            return;
        }

        _ = _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(EndCurrentCore));
    }

    /// <summary>
    /// 清空待播队列并作废正在等待延时的大字（AC-50/AC-76：锁屏/睡眠时不补弹过时大字）。
    /// 递增代号使已出队但仍在 delay 的请求在延时结束后被丢弃；正在播放的大字由 <see cref="EndCurrent"/> 处理。
    /// </summary>
    public void ClearPending()
    {
        Interlocked.Increment(ref _generation);
        while (_queue.Reader.TryRead(out _))
        {
            // 丢弃队列中尚未开始的大字
        }
    }

    /// <summary>消费泵：串行播放队列。必须在 UI 线程启动（OnStartup）。</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        await foreach (var request in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ShowAsync(request, ct);
            }
            catch (OperationCanceledException)
            {
                // 退出路径
            }
            catch (Exception ex)
            {
                _logWarn($"OVL-W-3004 大字渲染失败，已跳过：{ex.Message}");
            }
        }
    }

    private void EndCurrentCore()
    {
        _currentSound?.Dispose();
        _currentAnimation?.EndEarly();
    }

    private async Task ShowAsync(BannerRequest request, CancellationToken ct)
    {
        var generation = Volatile.Read(ref _generation);

        // 触发后延时（delaySeconds）；系统睡眠不计入延时（Task.Delay 基于系统计时，睡眠期间不推进，AC-76）
        if (request.DelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(request.DelaySeconds), ct);
        }

        // 延时期间被 ClearPending（锁屏/睡眠）作废 → 不补弹过时大字（AC-50/AC-76）
        if (Volatile.Read(ref _generation) != generation)
        {
            return;
        }

        // 目标屏工作区（AC-48）
        var screen = ScreenAreaResolver.Resolve(request.TargetScreen);
        if (screen.FellBack)
        {
            _logWarn($"OVL-W-3004 目标屏 {request.TargetScreen} 不可用，已回退主屏");
        }

        // 排版（多行段落模型，架构 §3.2）
        BannerMetricsResult metrics;
        try
        {
            metrics = BannerMetrics.Calculate(request, screen.Area.Width, (text, size) => _measureText(text, size));
        }
        catch (FormatException)
        {
            _logWarn("OVL-W-3004 大字排版失败：timeFormat 非法或占位符解析失败，已跳过");
            return;
        }
        catch (ArgumentException ex)
        {
            _logWarn($"OVL-W-3004 大字排版失败：{ex.Message}，已跳过");
            return;
        }

        // 视觉构建 + 窗口
        var visual = BannerVisualFactory.Build(request, metrics, screen.Area);
        var window = new BannerWindow(screen.Area, visual.Root);

        // 提示音（F-08，默认关；仅 PlaySound=true 时播放）
        if (request.PlaySound)
        {
            _currentSound = new BannerSoundPlayer();
            _currentSound.Play("system", null, 60);
        }

        // 帧率监控（AC-67/68/73）：低帧降档剥离描边 + 恢复
        var fps = new FrameRateMonitor(thresholdFps: 45, degradeAfterSeconds: 3, recoverAfterSeconds: 3);
        fps.Degraded += () =>
        {
            visual.StripEffects();
            _logWarn("OVL-W-3007 渲染帧率持续低于阈值，已剥离描边特效降档");
        };
        fps.Recovered += visual.RestoreEffects;
        if (_onFpsSample is not null)
        {
            fps.Sampled += (f, d) => _onFpsSample(f, d);
        }

        var animation = new BannerAnimation(visual.Block, request.FadeInMs, request.HoldSeconds, request.FadeOutMs);
        _currentAnimation = animation;

        // 渲染瞬时开销采样基线（AC-95）
        Process? renderProcess = null;
        TimeSpan cpu0 = default;
        long mem0 = 0;
        if (_onRenderCost is not null)
        {
            renderProcess = Process.GetCurrentProcess();
            cpu0 = renderProcess.TotalProcessorTime;
            mem0 = renderProcess.WorkingSet64;
        }

        // 显示并播放
        window.Show();
        fps.Start();
        animation.Play();
        _onPlayingChanged?.Invoke(true);
        if (renderProcess is not null)
        {
            _ = MeasureRenderCostAsync(renderProcess, cpu0, mem0, Math.Max(100, request.FadeInMs), ct);
        }

        try
        {
            await animation.CompletedTask;
        }
        finally
        {
            _onPlayingChanged?.Invoke(false);

            // 清理
            _currentAnimation = null;
            _currentSound?.Dispose();
            _currentSound = null;
            fps.Dispose();
            window.Close();
        }
    }

    /// <summary>浮现阶段渲染开销采样（AC-95）：延时后计算本进程 CPU/内存增量并回调，随后释放进程句柄。</summary>
    private async Task MeasureRenderCostAsync(Process process, TimeSpan cpu0, long mem0, int delayMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct);
            var cpuMs = (process.TotalProcessorTime - cpu0).TotalMilliseconds;
            var memMb = Math.Max(0, (process.WorkingSet64 - mem0) / 1024.0 / 1024.0);
            _onRenderCost?.Invoke(cpuMs, memMb);
        }
        catch (OperationCanceledException)
        {
            // 退出路径
        }
        catch (Exception)
        {
            // 采样失败不影响渲染
        }
        finally
        {
            process.Dispose();
        }
    }
}

/// <summary>
/// FormattedText 文本测量（像素每 DIP 固定 1.0：FontSize/排版均以 DIP 计，返回 DIP 宽）。
/// </summary>
public static class TextMeasure
{
    /// <summary>测量给定字体在指定字号下的渲染宽度（DIP）。</summary>
    public static double Measure(string text, double fontSize)
    {
        // 字重必须与渲染一致（Bold）：测量偏窄会导致 Wrap/Shrink 排版溢出（M2 视觉验收反馈）
        var typeface = new Typeface(
            FontAssets.BannerFont,
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            pixelsPerDip: 1.0);

        return formatted.Width;
    }
}