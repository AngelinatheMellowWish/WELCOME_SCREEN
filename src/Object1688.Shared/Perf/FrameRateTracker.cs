namespace Object1688.Shared.Perf;

/// <summary>
/// 最近一次帧率采样持有者（AC-67/AC-68 可机测）。
/// Overlay 每秒上报 FrameRate，经 Main 记录；控制接口 <c>status</c> 暴露最近值，
/// 供 M6 验收脚本自动读取「动画 ≥60fps」指标（无需人工目测）。
/// 置于 Shared 以便单元测试。
/// </summary>
public sealed class FrameRateTracker
{
    private readonly object _sync = new();
    private double _fps = -1;
    private bool _degraded;

    /// <summary>是否已收到至少一次帧率采样（未上报时为 false）。</summary>
    public bool HasValue
    {
        get
        {
            lock (_sync)
            {
                return _fps >= 0;
            }
        }
    }

    /// <summary>最近一次帧率（未上报时为 -1）。</summary>
    public double Fps
    {
        get
        {
            lock (_sync)
            {
                return _fps;
            }
        }
    }

    /// <summary>最近一次采样是否处于降级态。</summary>
    public bool Degraded
    {
        get
        {
            lock (_sync)
            {
                return _degraded;
            }
        }
    }

    /// <summary>记录一次帧率采样。</summary>
    /// <param name="fps">本次采样帧率。</param>
    /// <param name="degraded">本次采样是否处于降级态。</param>
    public void Update(double fps, bool degraded)
    {
        lock (_sync)
        {
            _fps = fps;
            _degraded = degraded;
        }
    }
}
