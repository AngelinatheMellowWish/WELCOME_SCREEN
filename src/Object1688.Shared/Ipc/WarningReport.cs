namespace Object1688.Shared.Ipc;

/// <summary>
/// 子进程警告上报（Overlay→Main，F-42 分层错误提示 / NFR-04 扩展）。
/// 用于 Overlay 侧无法自行弹托盘气泡的告警（如 OVL-W-3008 提权窗口/安全桌面无法覆盖），
/// Main 收到后经去重弹一次托盘气泡。
/// </summary>
public sealed class WarningReport
{
    /// <summary>错误码（如 OVL-W-3008）。</summary>
    public required string ErrorCode { get; init; }

    /// <summary>提示消息。</summary>
    public required string Message { get; init; }
}
