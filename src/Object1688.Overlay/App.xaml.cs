using System.Windows;
using Object1688.Overlay.Monitoring;
using Object1688.Overlay.Rendering;
using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Crash;
using Object1688.Shared.Ipc;
using Object1688.Shared.Logging;
using Object1688.Shared.Monitor;
using Object1688.Shared.Text;

namespace Object1688.Overlay;

/// <summary>
/// 叠加层进程入口（M1 骨架 + M2 大字渲染）。
/// 连接 Main 控制管道 → 握手 → 周期心跳 → 响应优雅退出（回执后退出码 0）；
/// 接收 TriggerCommand（渲染大字，队列串行）/ EndBanner（F-07 提前结束）/ Shutdown。
/// M2 冒烟：`--demo-banner` 参数直接展示欢迎大字，供视觉验收（不入 IPC）。
/// </summary>
public partial class App : Application
{
    private const string Module = "overlay";

    private readonly CancellationTokenSource _lifetimeCts = new();
    private BannerQueueService? _bannerQueue;

    /// <summary>主控制管道客户端（RunWorkerAsync 建立后赋值；供大字播放状态上报，F-07）。</summary>
    private volatile IpcPipeClient? _client;

    /// <summary>监测循环（供睡眠唤醒暂停，AC-41）。</summary>
    private MonitorLoop? _monitorLoop;

    /// <summary>提权窗口降级提示是否已发出（一次性，AC-94）。</summary>
    private int _uncoverableWarned;

    /// <summary>当前生效配置（M3c 引导用默认；M3d 起由 Main 的 ConfigChanged 广播替换为最新实例，监测循环按引用变化热重建引擎）。</summary>
    private volatile AppConfig _config = ConfigLoader.CreateDefault();

    /// <summary>demo 模式（--demo-banner）：仅视觉验收，不启动监测循环。</summary>
    private bool _isDemoMode;

    /// <summary>WPF 启动入口（UI 线程）：初始化队列泵（UI 线程串行消费）。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CrashGuard.Install("overlay");

        _bannerQueue = new BannerQueueService(
            Dispatcher,
            message => _ = LogAsync(LogLevel.Warn, message, CancellationToken.None),
            TextMeasure.Measure,
            OnBannerPlayingChanged,
            OnFpsSampled,
            OnRenderCostSampled);
        _ = _bannerQueue.RunAsync(_lifetimeCts.Token);

        var demoArg = e.Args.FirstOrDefault(a => a.StartsWith("--demo-banner", StringComparison.OrdinalIgnoreCase));
        if (demoArg is not null)
        {
            _isDemoMode = true;
            _bannerQueue.Enqueue(CreateDemoBanner(ResolveDemoCase(demoArg)));
        }

        _ = Task.Run(() => RunWorkerAsync(_lifetimeCts.Token));
    }

    /// <summary>应用退出：取消生命周期令牌。</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _lifetimeCts.Cancel();
        base.OnExit(e);
    }

    private async Task RunWorkerAsync(CancellationToken ct)
    {
        var handshake = CreateHandshake();
        await LogAsync(LogLevel.Info, "Overlay 进程启动", ct);

        await using var client = new IpcPipeClient(IpcProtocol.MainControlPipe, handshake);
        _client = client;
        client.MessageReceived += (_, envelope) =>
        {
            switch (envelope.Type)
            {
                case IpcMessageType.Shutdown:
                    _ = HandleShutdownAsync(client);
                    break;

                case IpcMessageType.TriggerCommand:
                    var request = envelope.GetPayload<BannerRequest>();
                    if (request is not null)
                    {
                        _bannerQueue?.Enqueue(request);
                    }
                    else
                    {
                        _ = LogAsync(LogLevel.Warn, "收到 TriggerCommand 但载荷解析失败（BannerRequest 为 null），已忽略", CancellationToken.None);
                    }

                    break;

                case IpcMessageType.ConfigChanged:
                    var appConfig = envelope.GetPayload<AppConfig>();
                    if (appConfig is not null)
                    {
                        _config = appConfig; // 引用替换：监测循环下一轮按新配置重建引擎
                        _ = LogAsync(LogLevel.Info, "收到 ConfigChanged，配置已更新（监测引擎将按新配置重建）", CancellationToken.None);
                    }
                    else
                    {
                        _ = LogAsync(LogLevel.Warn, "收到 ConfigChanged 但载荷解析失败（AppConfig 为 null），已忽略", CancellationToken.None);
                    }

                    break;

                case IpcMessageType.EndBanner:
                    _bannerQueue?.EndCurrent();
                    break;

                case IpcMessageType.SystemEvent:
                    var systemEvent = envelope.GetPayload<SystemEventReport>();
                    if (systemEvent is not null)
                    {
                        HandleSystemEvent(systemEvent);
                    }

                    break;
            }
        };

        // 接收循环后台运行（断线自动重连）
        _ = client.RunAsync(ct);

        // M3c 监测循环（架构 §4.1）：demo 模式跳过（仅视觉验收）
        if (!_isDemoMode)
        {
            StartMonitorLoop(client, ct);
        }

        // 心跳循环：HeartbeatInterval(2s) 上报存活，直至退出
        using var timer = new PeriodicTimer(IpcProtocol.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await client.SendAsync(IpcMessageType.Heartbeat, null, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出路径
        }
    }

    private async Task HandleShutdownAsync(IpcPipeClient client)
    {
        await LogAsync(LogLevel.Info, "收到退出指令，Overlay 进程退出", CancellationToken.None);
        try
        {
            await client.SendAsync(IpcMessageType.ShutdownAck, null, CancellationToken.None);
        }
        catch
        {
            // 回执失败不阻塞退出（Main 侧 3s 后会强制 Kill）
        }

        Dispatcher.Invoke(() => Shutdown(0));
    }

    /// <summary>M3c 监测循环启动：周期快照 → 引擎评估 → 命中经 TriggerEvent 上报 Main（架构 §4.4）。</summary>
    private void StartMonitorLoop(IpcPipeClient client, CancellationToken ct)
    {
        var monitorLoop = new MonitorLoop(
            () => _config,
            new WindowsMonitorProvider(),
            AppContext.BaseDirectory,
            onTrigger: trigger => _ = SendTriggerEventAsync(client, trigger, ct),
            onSuppression: suppression => _ = LogSuppressionAsync(suppression, CancellationToken.None),
            onError: message => _ = LogAsync(LogLevel.Warn, $"{ErrorCodes.MonitorDetectionFailed} 监测快照采集失败：{message}", CancellationToken.None),
            onFullscreenChanged: OnFullscreenChanged);

        _monitorLoop = monitorLoop;
        _ = Task.Run(() => monitorLoop.RunAsync(ct));
    }

    /// <summary>命中上报：组装 TriggerEventReport 发送 TriggerEvent；失败不阻塞监测循环（下轮重试）。</summary>
    private async Task SendTriggerEventAsync(IpcPipeClient client, MonitorTrigger trigger, CancellationToken ct)
    {
        // 提权窗口降级（AC-94）：目标进程为提升完整性级别 → Overlay（asInvoker）无法覆盖，记 OVL-W-3008 + 一次性托盘提示
        if (Volatile.Read(ref _uncoverableWarned) == 0 && ElevationProbe.IsProcessElevated(trigger.Target.ProcessId))
        {
            Interlocked.Exchange(ref _uncoverableWarned, 1);
            await LogAsync(
                LogLevel.Warn,
                $"{ErrorCodes.OverlaySecureDesktopBlocked} 目标为提权窗口/安全桌面，无法覆盖：{trigger.Target.Text}（pid={trigger.Target.ProcessId}）",
                ct);
            try
            {
                await client.SendAsync(IpcMessageType.Warning, new WarningReport
                {
                    ErrorCode = ErrorCodes.OverlaySecureDesktopBlocked,
                    Message = "目标为提权窗口/安全桌面，无法覆盖（详见日志）",
                }, ct);
            }
            catch (OperationCanceledException)
            {
                // 退出路径
            }
            catch (Exception)
            {
                // 上报失败不影响监测
            }
        }

        var report = new TriggerEventReport
        {
            RuleId = trigger.Rule.RuleId,
            MatchType = trigger.Rule.MatchType,
            MatchValue = trigger.Rule.MatchValue,
            MatchedText = trigger.Target.Text,
            ProcessId = trigger.Target.ProcessId,
            IsFullscreen = trigger.Target.IsFullscreen,
            IsReappearTrigger = trigger.IsReappearTrigger,
        };

        await LogAsync(LogLevel.Info, $"{ErrorCodes.MonitorRuleHit} 规则 [{trigger.Rule.RuleId}] 命中：{trigger.Target.Text}（pid={trigger.Target.ProcessId}）", CancellationToken.None);

        try
        {
            await client.SendAsync(IpcMessageType.TriggerEvent, report, ct);
        }
        catch (OperationCanceledException)
        {
            // 退出路径：忽略
        }
        catch (Exception)
        {
            // SendAsync 内部尽力投递，失败不阻塞监测循环
        }
    }

    /// <summary>大字播放状态变化（F-07）：上报 Main，供手动触发快捷键"再次按下提前结束"裁决。</summary>
    private void OnBannerPlayingChanged(bool playing)
    {
        var client = _client;
        if (client is not null)
        {
            _ = SendBannerStateAsync(client, playing);
        }
    }

    /// <summary>渲染瞬时开销（AC-95）：超阈值记 OVL-W-3007，并上报 Main→Monitor。</summary>
    private void OnRenderCostSampled(double cpuMs, double memMb)
    {
        var global = _config.Global;
        if (cpuMs > global.RenderCostCpuWarnMs || memMb > global.RenderCostMemWarnMB)
        {
            _ = LogAsync(LogLevel.Warn, $"OVL-W-3007 大字触发瞬间渲染开销超阈值：CPU {cpuMs:F0}ms（阈值 {global.RenderCostCpuWarnMs}ms）/ 内存 +{memMb:F1}MB（阈值 {global.RenderCostMemWarnMB}MB）", CancellationToken.None);
        }

        var client = _client;
        if (client is not null)
        {
            _ = SendRenderCostAsync(client, cpuMs, memMb);
        }
    }

    /// <summary>发送渲染瞬时开销（Overlay→Main→Monitor，NFR-02 扩展/AC-95）。</summary>
    private async Task SendRenderCostAsync(IpcPipeClient client, double cpuMs, double memMb)
    {
        try
        {
            await client.SendAsync(IpcMessageType.RenderCost, new RenderCostReport { CpuMs = cpuMs, MemMB = memMb }, _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 退出路径
        }
        catch (Exception)
        {
            // 上报失败不影响渲染
        }
    }

    /// <summary>投屏/演示状态变化（AC-90）：上报 Main，Main 在开关开启时静默自动触发。</summary>
    private void OnFullscreenChanged(bool active)
    {
        var client = _client;
        if (client is not null)
        {
            _ = SendPresentationStateAsync(client, active);
        }
    }

    /// <summary>发送投屏/演示状态（Overlay→Main，F-25 扩展/AC-90）。</summary>
    private async Task SendPresentationStateAsync(IpcPipeClient client, bool active)
    {
        try
        {
            await client.SendAsync(IpcMessageType.PresentationState, new PresentationStateReport { Active = active }, _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 退出路径
        }
        catch (Exception)
        {
            // 上报失败不影响监测
        }
    }

    /// <summary>系统电源/会话事件（架构 §2.4）：睡眠清空待播、唤醒延迟恢复监测、锁屏结束队列且不补发。</summary>
    private void HandleSystemEvent(SystemEventReport report)
    {
        switch (report.Kind)
        {
            case SystemEventKind.Suspend:
                _bannerQueue?.ClearPending();
                _ = LogAsync(LogLevel.Info, "系统睡眠：已清空待播大字（AC-76）", CancellationToken.None);
                break;

            case SystemEventKind.Resume:
                _monitorLoop?.PauseFor(TimeSpan.FromSeconds(3));
                _ = LogAsync(LogLevel.Info, "系统唤醒：监测暂停 3s 抑制枚举抖动后恢复（AC-41）", CancellationToken.None);
                break;

            case SystemEventKind.Lock:
                _bannerQueue?.EndCurrent();
                _bannerQueue?.ClearPending();
                _ = LogAsync(LogLevel.Info, "会话锁定：已结束当前大字并清空队列，解锁后不补发（AC-50）", CancellationToken.None);
                break;

            case SystemEventKind.Unlock:
                _ = LogAsync(LogLevel.Info, "会话解锁：不补发大字（AC-50）", CancellationToken.None);
                break;
        }
    }

    /// <summary>发送大字播放状态（Overlay→Main，F-07/AC-30）。</summary>
    private async Task SendBannerStateAsync(IpcPipeClient client, bool playing)
    {
        try
        {
            await client.SendAsync(IpcMessageType.BannerState, new BannerStateReport { Playing = playing }, _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 退出路径
        }
        catch (Exception)
        {
            // 上报失败不影响渲染
        }
    }

    /// <summary>每秒帧率采样（AC-68）：上报 Main，Main 转 Monitor 绘制帧率曲线。</summary>
    private void OnFpsSampled(double fps, bool degraded)
    {
        var client = _client;
        if (client is not null)
        {
            _ = SendFrameRateAsync(client, fps, degraded);
        }
    }

    /// <summary>发送帧率采样（Overlay→Main→Monitor，NFR-02/AC-68）。</summary>
    private async Task SendFrameRateAsync(IpcPipeClient client, double fps, bool degraded)
    {
        try
        {
            await client.SendAsync(IpcMessageType.FrameRate, new FrameRateReport { Fps = fps, Degraded = degraded }, _lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 退出路径
        }
        catch (Exception)
        {
            // 上报失败不影响渲染
        }
    }

    /// <summary>抑制日志：minInterval 记 MON-W-5005；进程存活期去重为正常语义（Debug，不记错误码）。</summary>
    private static Task LogSuppressionAsync(MonitorSuppression suppression, CancellationToken ct)
    {
        if (suppression.Reason == SuppressionReason.MinInterval)
        {
            return LogAsync(LogLevel.Warn, $"{ErrorCodes.MonitorRuleSuppressed} 规则 [{suppression.Rule.RuleId}] 命中被 minInterval 抑制（{suppression.Target.Text}）", ct);
        }

        return LogAsync(LogLevel.Debug, $"规则 [{suppression.Rule.RuleId}] 命中被进程存活期去重（{suppression.Target.Text}）", ct);
    }

    /// <summary>M2 视觉验收演示大字（架构 F-02/F-11）。`--demo-banner[=case]` 选择用例，缺省 classic。</summary>
    private static BannerRequest CreateDemoBanner(string caseId)
    {
        const double delay = 1;
        const double hold = 4;

        switch (caseId)
        {
            case "multiline":
                // 中英混排 + 多行独立字号/颜色/对齐（F-06/AC-19/AC-43/AC-88）
                return new BannerRequest
                {
                    DisplayLines = new[]
                    {
                        new DisplayLine { Text = "浏览器 Browser", FontSize = 120, Color = "#FFFFFF" },
                        new DisplayLine { Text = "新标签页已打开", FontSize = 56, Color = "#CCCCCC" },
                        new DisplayLine { Text = "Tab opened · {time}", FontSize = 40, Color = "#999999" },
                    },
                    OutlineColor = "#000000",
                    OutlineWidth = 3,
                    DelaySeconds = delay,
                    HoldSeconds = hold,
                    WrapStrategy = WrapStrategy.Wrap,
                };

            case "outline":
                return FromPreset(StylePresets.ControlHighContrast, "描边测试 Outline 描边", delay, hold);

            case "subtitle":
                return FromPreset(StylePresets.SubtitleLight, "字幕风格 Subtitle", delay, hold);

            case "cinema":
                return FromPreset(StylePresets.CinemaDark, "影院风格 Cinema", delay, hold);

            case "custom":
                // 自定义归一化定位（架构 §4.2 custom {x,y}，AC-48）
                return new BannerRequest
                {
                    DisplayLines = new[] { new DisplayLine { Text = "自定义位置 Custom (0.5, 0.22)", FontSize = 80 } },
                    Color = "#FFFFFF",
                    OutlineColor = "#000000",
                    OutlineWidth = 3,
                    Position = "custom",
                    CustomX = 0.5,
                    CustomY = 0.22,
                    DelaySeconds = delay,
                    HoldSeconds = hold,
                    WrapStrategy = WrapStrategy.Wrap,
                };

            case "long":
                // 超宽 CJK 文本字符级硬折（AC-43 wrap）
                return new BannerRequest
                {
                    DisplayLines = new[] { new DisplayLine { Text = "这是一段用于验证中文自动换行与字符级硬折排版效果的超长标题文本，包含中英文 mixed Latin words 混排内容。", FontSize = 96 } },
                    Color = "#FFFFFF",
                    OutlineColor = "#000000",
                    OutlineWidth = 3,
                    DelaySeconds = delay,
                    HoldSeconds = hold,
                    WrapStrategy = WrapStrategy.Wrap,
                };

            default:
                return FromPreset(StylePresets.ControlClassic, "欢迎使用 Object1688", delay, hold);
        }
    }

    /// <summary>按预设参数构建演示请求（预设仅提供块级样式起点）。</summary>
    private static BannerRequest FromPreset(StylePreset preset, string text, double delay, double hold) => new()
    {
        DisplayLines = new[] { new DisplayLine { Text = text } },
        FontSize = preset.FontSize,
        Color = preset.Color,
        OutlineColor = preset.OutlineColor,
        OutlineWidth = preset.OutlineWidth,
        Position = preset.Position,
        DelaySeconds = delay,
        HoldSeconds = hold,
        WrapStrategy = WrapStrategy.Wrap,
    };

    /// <summary>解析 `--demo-banner[=case]` 的用例名（无值/空值 → classic）。</summary>
    private static string ResolveDemoCase(string arg)
    {
        var idx = arg.IndexOf('=');
        return idx >= 0 && idx < arg.Length - 1
            ? arg[(idx + 1)..].Trim().ToLowerInvariant()
            : "classic";
    }

    private static IpcHandshake CreateHandshake() => new()
    {
        Role = IpcRole.Overlay,
        Pid = Environment.ProcessId,
        ExecutablePath = Environment.ProcessPath ?? string.Empty,
    };

    private static async Task LogAsync(LogLevel level, string message, CancellationToken ct, int timeoutMs = 2000)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await using var logClient = new IpcPipeClient(IpcProtocol.LoggingPipe, CreateHandshake());
            _ = logClient.RunAsync(cts.Token);
            await logClient.SendAsync(
                IpcMessageType.LogEntry,
                new LogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = level,
                    Module = Module,
                    Message = message,
                },
                cts.Token);
            await logClient.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // Logging 不可达（M1 最佳努力，不阻塞主流程）
        }
        catch (Exception)
        {
            // 同上
        }
    }
}