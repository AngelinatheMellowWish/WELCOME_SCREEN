namespace Object1688.Shared.Ipc;

/// <summary>
/// IPC 消息类型（架构 §2.3 协议定版）。
/// 枚举名即线上协议字符串（JSON 序列化启用枚举转字符串），禁止更改既有名称。
/// </summary>
public enum IpcMessageType
{
    /// <summary>握手（客户端→服务端，首条消息，载荷为 IpcHandshake）。</summary>
    Handshake,

    /// <summary>握手确认（服务端→客户端）。</summary>
    HandshakeAck,

    /// <summary>心跳（子进程→Main，周期上报存活）。</summary>
    Heartbeat,

    /// <summary>状态上报（子进程→Main，当前运行状态快照）。</summary>
    StatusReport,

    /// <summary>配置变更通知（ConfigUI→Main / Main 广播）。</summary>
    ConfigChanged,

    /// <summary>触发命令（Main→Overlay，推动大字显示，载荷为 BannerRequest）。</summary>
    TriggerCommand,

    /// <summary>触发命中上报（Overlay→Main，监测命中转交调度，载荷为 TriggerEventReport，架构 §4.4）。</summary>
    TriggerEvent,

    /// <summary>提前结束当前大字（Main→Overlay，F-07：浮现/保持阶段立即切入淡出并出队）。</summary>
    EndBanner,

    /// <summary>日志条目（任意进程→Logging，载荷为 LogEntry）。</summary>
    LogEntry,

    /// <summary>优雅退出命令（Main→子进程，载荷为 ShutdownReason）。</summary>
    Shutdown,

    /// <summary>退出回执（子进程→Main，确认已保存并准备退出）。</summary>
    ShutdownAck,

    /// <summary>二次实例参数转发（二次实例→既有 Main，F-73/AC-78，载荷为参数数组）。</summary>
    RedirectArgs,

    /// <summary>测试显示命令（ConfigUI→Main，F-23：三类大字共用的"测试播放"；载荷为 BannerRequest，Main 校验来源后转 TriggerCommand 下发 Overlay）。</summary>
    TestPlay,

    /// <summary>打开性能窗口（Main→Monitor，托盘"性能窗口…"入口；载荷 null）。</summary>
    ShowPerfWindow,

    /// <summary>大字播放状态上报（Overlay→Main，F-07：是否正在显示大字，供快捷键"再次按下提前结束"裁决；载荷为 BannerStateReport）。</summary>
    BannerState,

    /// <summary>渲染帧率上报（Overlay→Main→Monitor，NFR-02/AC-68；载荷为 FrameRateReport）。</summary>
    FrameRate,

    /// <summary>最近一次大字回看（Main→Monitor，F-30 扩展/AC-92；载荷为 LastBannerReport）。</summary>
    LastBanner,

    /// <summary>系统电源/会话事件（Main→Overlay/Monitor，架构 §2.4：睡眠唤醒/锁屏会话，AC-41/50/76；载荷为 SystemEventReport）。</summary>
    SystemEvent,

    /// <summary>投屏/演示状态上报（Overlay→Main，F-25 扩展/AC-90；载荷为 PresentationStateReport）。</summary>
    PresentationState,

    /// <summary>渲染瞬时开销上报（Overlay→Main→Monitor，NFR-02 扩展/AC-95；载荷为 RenderCostReport）。</summary>
    RenderCost,

    /// <summary>子进程警告上报（Overlay→Main，F-42/NFR-04 扩展：提权窗口/安全桌面无法覆盖等；载荷为 WarningReport，Main 去重弹托盘气泡）。</summary>
    Warning,
}