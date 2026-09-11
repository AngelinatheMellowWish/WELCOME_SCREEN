namespace Object1688.Shared.Ipc;

/// <summary>
/// 投屏/演示状态上报（Overlay→Main，F-25 扩展/AC-90）。
/// Overlay 监测到全屏演示/投屏（存在真实全屏窗口）时上报，Main 在开启
/// <c>dnd.presentationAutoSilence</c> 时据此静默自动触发大字（手动/测试为强制路径）。
/// </summary>
public sealed class PresentationStateReport
{
    /// <summary>当前是否存在全屏演示/投屏窗口。</summary>
    public required bool Active { get; init; }
}
