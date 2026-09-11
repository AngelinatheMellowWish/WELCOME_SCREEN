namespace Object1688.Shared.Ipc;

/// <summary>
/// 进程角色（六进程架构，架构 §2.2）。
/// 用于握手与路由寻址；枚举名即协议字符串，禁止更改既有名称。
/// </summary>
public enum IpcRole
{
    /// <summary>主进程（控制器，唯一服务端）。</summary>
    Main,

    /// <summary>日志进程（唯一日志落盘服务）。</summary>
    Logging,

    /// <summary>渲染/叠加进程（大字显示）。</summary>
    Overlay,

    /// <summary>监测进程（窗口/进程规则匹配）。</summary>
    Monitor,

    /// <summary>配置界面进程（按需拉起）。</summary>
    ConfigUI,
}