namespace Object1688.Shared.Ipc;

/// <summary>
/// 大字播放状态上报（Overlay→Main，F-07/AC-30）。
/// 供 Main 在手动触发快捷键"再次按下"时裁决：正在显示 → 提前结束；否则 → 触发手动大字。
/// </summary>
public sealed class BannerStateReport
{
    /// <summary>是否有大字正在显示。</summary>
    public required bool Playing { get; init; }
}
