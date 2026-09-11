using Object1688.Shared.Ipc;

namespace Object1688.Shared.Perf;

/// <summary>帧率数据点（AC-68）。</summary>
public readonly record struct FrameRatePoint(DateTimeOffset Time, double Fps, bool Degraded);

/// <summary>渲染瞬时开销数据点（AC-95）。</summary>
public readonly record struct RenderCostPoint(DateTimeOffset Time, double CpuMs, double MemMB);

/// <summary>
/// 性能监控运行时数据汇聚（AC-68 帧率曲线 + AC-92 最近一次大字回看 + AC-95 渲染瞬时开销）。
/// 由 Monitor 接收 Main 转发的 FrameRate / LastBanner / RenderCost 消息写入，供性能窗口展示。
/// 置于 Shared 以便单元测试（RingBuffer 同层）。
/// </summary>
public sealed class MonitorFeed
{
    /// <summary>帧率曲线环形缓冲（5 分钟 @1s）。</summary>
    public RingBuffer<FrameRatePoint> FrameRates { get; } = new(300);

    /// <summary>最近 N 次渲染瞬时开销（AC-95）。</summary>
    public RingBuffer<RenderCostPoint> RenderCosts { get; } = new(50);

    private volatile LastBannerReport? _lastBanner;

    /// <summary>最近一次实际下发的大字（AC-92）。</summary>
    public LastBannerReport? LastBanner
    {
        get => _lastBanner;
        set => _lastBanner = value;
    }

    /// <summary>追加帧率采样点。</summary>
    public void AddFrameRate(double fps, bool degraded)
        => FrameRates.Append(new FrameRatePoint(DateTimeOffset.UtcNow, fps, degraded));

    /// <summary>追加渲染瞬时开销采样点。</summary>
    public void AddRenderCost(double cpuMs, double memMB)
        => RenderCosts.Append(new RenderCostPoint(DateTimeOffset.UtcNow, cpuMs, memMB));
}
