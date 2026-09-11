namespace Object1688.Shared.Ipc;

/// <summary>
/// 优雅退出的原因（架构 §2.4，Shutdown 消息载荷）。
/// </summary>
public enum ShutdownReason
{
    /// <summary>用户主动退出（托盘"退出"）。</summary>
    UserExit,

    /// <summary>用户退出且本次取消开机自启（需先移除自启项再广播）。</summary>
    UserExitNoAutostart,

    /// <summary>系统会话结束（注销/关机，Windows 会话结束事件）。</summary>
    SessionEnding,
}