namespace Object1688.Shared.Ipc;

/// <summary>
/// 握手载荷（客户端连接建立后首条消息，AC-70）。
/// 服务端校验 Role + Pid 归属受信进程集合，校验失败拒绝连接并记 IPC-E-7003。
/// </summary>
public sealed class IpcHandshake
{
    /// <summary>连接方进程角色。</summary>
    public required IpcRole Role { get; init; }

    /// <summary>连接方进程 PID。</summary>
    public required int Pid { get; init; }

    /// <summary>进程可执行文件完整路径（供服务端校验归属同程序目录）。</summary>
    public required string ExecutablePath { get; init; }
}