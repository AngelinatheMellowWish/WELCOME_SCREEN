using System.Windows.Media;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 渲染帧率监控（架构 §3.2 帧率与特效降级，NFR-02/AC-67/AC-68/AC-73）。
/// 基于 WPF 渲染线程计时（CompositionTarget.Rendering）统计实际帧率：
/// 持续低于阈值（典型 &lt;45fps，可配）→ 触发 <see cref="Degraded"/>（调用方关闭阴影等特效并记 OVL-W-3007 日志）；
/// 帧率回升持续 N 秒 → 触发 <see cref="Recovered"/>（自动回全特效）。
/// 单个大字显示周期内创建，显示结束 Dispose 摘除渲染事件钩子。
/// </summary>
public sealed class FrameRateMonitor : IDisposable
{
    private readonly double _thresholdFps;
    private readonly int _degradeAfterSeconds;
    private readonly int _recoverAfterSeconds;

    private readonly object _sync = new();
    private DateTime _windowStart;
    private int _framesInWindow;
    private int _lowStreak;
    private int _okStreak;
    private bool _degraded;
    private bool _disposed;

    /// <summary>帧率低于阈值持续达到降级秒数（触发一次）。</summary>
    public event Action? Degraded;

    /// <summary>帧率回升持续达到恢复秒数（触发一次）。</summary>
    public event Action? Recovered;

    /// <summary>每秒帧率采样（fps, 当前是否降级）——供上报 Monitor 绘制帧率曲线（AC-68）。</summary>
    public event Action<double, bool>? Sampled;

    /// <param name="thresholdFps">帧率阈值（默认 45）。</param>
    /// <param name="degradeAfterSeconds">低帧持续秒数才判定降级（默认 3）。</param>
    /// <param name="recoverAfterSeconds">帧率回升持续秒数才恢复（默认 3）。</param>
    public FrameRateMonitor(double thresholdFps = 45, int degradeAfterSeconds = 3, int recoverAfterSeconds = 3)
    {
        _thresholdFps = thresholdFps;
        _degradeAfterSeconds = Math.Max(1, degradeAfterSeconds);
        _recoverAfterSeconds = Math.Max(1, recoverAfterSeconds);
    }

    /// <summary>开始统计（挂载渲染事件）。需在 UI 线程调用。</summary>
    public void Start()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _windowStart = DateTime.UtcNow;
            _framesInWindow = 0;
            _lowStreak = 0;
            _okStreak = 0;
            _degraded = false;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    /// <summary>取消统计并摘除渲染事件钩子。</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        bool degradedNow = false;
        bool recoveredNow = false;
        double fpsSample = 0;
        var sampled = false;
        bool degradedState = false;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _framesInWindow++;
            var elapsed = (DateTime.UtcNow - _windowStart).TotalSeconds;
            if (elapsed < 1.0)
            {
                return;
            }

            var fps = _framesInWindow / elapsed;
            _framesInWindow = 0;
            _windowStart = DateTime.UtcNow;
            fpsSample = fps;
            sampled = true;

            if (fps < _thresholdFps)
            {
                _lowStreak++;
                _okStreak = 0;
            }
            else
            {
                _okStreak++;
                _lowStreak = 0;
            }

            if (!_degraded && _lowStreak >= _degradeAfterSeconds)
            {
                _degraded = true;
                degradedNow = true;
            }
            else if (_degraded && _okStreak >= _recoverAfterSeconds)
            {
                _degraded = false;
                _lowStreak = 0;
                _okStreak = 0;
                recoveredNow = true;
            }

            degradedState = _degraded;
        }

        // 事件在锁外触发，避免回调重入锁
        if (sampled)
        {
            Sampled?.Invoke(fpsSample, degradedState);
        }

        if (degradedNow)
        {
            Degraded?.Invoke();
        }
        else if (recoveredNow)
        {
            Recovered?.Invoke();
        }
    }
}