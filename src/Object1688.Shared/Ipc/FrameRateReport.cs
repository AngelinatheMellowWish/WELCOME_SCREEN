namespace Object1688.Shared.Ipc;

/// <summary>
/// 渲染帧率上报（Overlay→Main→Monitor，NFR-02/AC-68）。
/// Overlay 每 1s 聚合上报一次当前动画帧率与降级状态，供性能窗口绘制帧率曲线。
/// </summary>
public sealed class FrameRateReport
{
    /// <summary>最近 1s 窗口平均帧率（fps）。</summary>
    public required double Fps { get; init; }

    /// <summary>当前是否处于特效降级状态（帧率持续低于阈值）。</summary>
    public required bool Degraded { get; init; }
}
