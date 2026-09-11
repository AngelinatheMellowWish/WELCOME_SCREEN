namespace Object1688.Shared.Ipc;

/// <summary>
/// 渲染瞬时开销上报（Overlay→Main→Monitor，NFR-02 扩展/AC-95）。
/// Overlay 在大字触发渲染瞬间（浮现阶段）采样本进程 CPU 时段增量与内存增量，供性能窗口展示；
/// 超阈值由 Overlay 记 OVL-W-3007 日志。
/// </summary>
public sealed class RenderCostReport
{
    /// <summary>触发瞬间 CPU 时间增量（毫秒）。</summary>
    public required double CpuMs { get; init; }

    /// <summary>触发瞬间内存增量（MB，可为负则钳 0）。</summary>
    public required double MemMB { get; init; }
}
