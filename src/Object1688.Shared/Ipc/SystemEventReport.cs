namespace Object1688.Shared.Ipc;

/// <summary>系统电源/会话事件类型（架构 §2.4 / NFR-03，AC-41/AC-50）。</summary>
public enum SystemEventKind
{
    /// <summary>系统进入睡眠。</summary>
    Suspend,

    /// <summary>系统从睡眠唤醒。</summary>
    Resume,

    /// <summary>会话锁定/断开（锁屏/远程断开/控制台断开）。</summary>
    Lock,

    /// <summary>会话解锁/连接。</summary>
    Unlock,
}

/// <summary>
/// 系统事件上报（Main→Overlay/Monitor，架构 §2.4）。
/// Main 监听 PowerBroadcast / SessionSwitch 后下发：睡眠唤醒恢复监测（AC-41/AC-76）、
/// 锁屏/会话切换立即结束播放队列且不补发（AC-50）。
/// </summary>
public sealed class SystemEventReport
{
    /// <summary>事件类型。</summary>
    public required SystemEventKind Kind { get; init; }
}
