namespace Object1688.Shared.Ipc;

/// <summary>
/// IPC 协议常量（架构 §2.3 协议定版）。
/// </summary>
public static class IpcProtocol
{
    /// <summary>协议版本号（v1）。信封中必须携带，版本不匹配的连接应拒绝。</summary>
    public const int Version = 1;

    /// <summary>Main 控制管道名（服务端：Main；客户端：全部子进程）。</summary>
    public const string MainControlPipe = "Object1688.main";

    /// <summary>Logging 日志管道名（服务端：Logging；客户端：Main + 其余子进程）。</summary>
    public const string LoggingPipe = "Object1688.logging";

    /// <summary>命名管道统一前缀（\\.\pipe\{前缀}{管道名}}）。</summary>
    public const string PipePrefix = @"\\.\pipe\";

    /// <summary>子进程心跳间隔。</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(2);

    /// <summary>心跳超时（超过该时长未收到心跳判定失联，Main 触发重启流程，IPC-W-7002）。</summary>
    public static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(6);

    /// <summary>Main 等待子进程优雅退出的总时限（超时后 Kill，PRC-E-2002）。</summary>
    public static readonly TimeSpan ShutdownGraceTimeout = TimeSpan.FromSeconds(3);

    /// <summary>会话结束时子进程落盘时限（架构 §2.4：2 秒内完成落盘）。</summary>
    public static readonly TimeSpan SessionEndingFlushTimeout = TimeSpan.FromSeconds(2);

    /// <summary>握手超时（连接建立后必须在该时限内完成握手，否则服务端断开）。</summary>
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>子进程崩溃重启退避上限（指数退避 1s→2s→4s→…→该上限，PRC-I-2003）。</summary>
    public static readonly TimeSpan RestartBackoffCap = TimeSpan.FromSeconds(30);
}