using Object1688.Shared.Crash;
using Object1688.Shared.Ipc;
using Object1688.Shared.Logging;

namespace Object1688.Logging;

/// <summary>
/// 日志进程入口（M1 骨架）。
/// 职责：作为"Object1688.logging"管道服务端收拢全进程日志条目并落盘 log\app.log；
/// 同时以客户端身份连接 Main 控制管道，参与握手/心跳/优雅退出协议。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 进程入口。
    /// </summary>
    /// <returns>退出码（0 = 正常退出）。</returns>
    private static async Task<int> Main()
    {
        CrashGuard.Install("logging");
        var logRoot = Path.Combine(AppContext.BaseDirectory, "log");
        Directory.CreateDirectory(logRoot);
        using var logFile = new LogFileWriter(logRoot, prefix: "app");

        var handshake = CreateHandshake();
        await using var controlClient = new IpcPipeClient(IpcProtocol.MainControlPipe, handshake);
        using var shutdownCts = new CancellationTokenSource();

        // 日志管道服务端：收拢各进程 LogEntry 并落盘
        await using var logServer = new IpcPipeServer(IpcProtocol.LoggingPipe, IsTrustedPeer);
        logServer.MessageReceived += (_, e) =>
        {
            if (e.Envelope.Type == IpcMessageType.LogEntry && e.Envelope.GetPayload<LogEntry>() is { } entry)
            {
                logFile.Append(entry.FormatLine());
            }
        };
        logServer.Start();

        // 控制通道：收到 Main 退出指令 → 回执 → 退出
        controlClient.MessageReceived += (_, envelope) =>
        {
            if (envelope.Type == IpcMessageType.Shutdown)
            {
                logFile.Append(CreateEntry(LogLevel.Info, "收到 Main 退出指令，准备退出").FormatLine());
                _ = controlClient.SendAsync(IpcMessageType.ShutdownAck, null, CancellationToken.None);
                shutdownCts.Cancel();
            }
        };

        // 连接成功/重连成功均上报（诊断辅助，不阻塞任何流程）
        controlClient.Connected += (_, _) =>
            logFile.Append(CreateEntry(LogLevel.Info, "已连接 Main 控制管道").FormatLine());

        // 控制通道连接循环后台运行（断线自动重连，永不完成直至取消）
        _ = controlClient.RunAsync(shutdownCts.Token);

        // 心跳循环：与连接循环并发——SendAsync 在未就绪时内部排队等待，连接建立后即开始上报
        using var heartbeatTimer = new PeriodicTimer(IpcProtocol.HeartbeatInterval);
        var heartbeatTask = Task.Run(async () =>
        {
            try
            {
                while (await heartbeatTimer.WaitForNextTickAsync(shutdownCts.Token))
                {
                    await controlClient.SendAsync(IpcMessageType.Heartbeat, null, shutdownCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常退出路径
            }
        }, CancellationToken.None);

        logFile.Append(CreateEntry(LogLevel.Info, "日志进程就绪").FormatLine());

        // 阻塞至收到退出指令
        try
        {
            await Task.Delay(Timeout.Infinite, shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 退出指令已触发
        }

        logFile.Flush();
        await heartbeatTask.ConfigureAwait(false);
        logFile.Append(CreateEntry(LogLevel.Info, "日志进程退出").FormatLine());
        return 0;
    }

    /// <summary>
    /// 构造本进程握手信息。
    /// </summary>
    private static IpcHandshake CreateHandshake() => new()
    {
        Role = IpcRole.Logging,
        Pid = Environment.ProcessId,
        ExecutablePath = Environment.ProcessPath ?? string.Empty,
    };

    /// <summary>
    /// 构造一条标准日志条目（本进程自身日志）。
    /// </summary>
    private static LogEntry CreateEntry(LogLevel level, string message) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = level,
        Module = "logging",
        Message = message,
    };

    /// <summary>
    /// AC-70 受信校验：仅接受可执行文件与自身同目录的进程连接。
    /// </summary>
    /// <param name="handshake">连接方握手信息。</param>
    /// <returns>true = 放行。</returns>
    private static bool IsTrustedPeer(IpcHandshake handshake)
    {
        var peerDir = Path.GetDirectoryName(handshake.ExecutablePath);
        var selfDir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
        return !string.IsNullOrEmpty(peerDir)
            && !string.IsNullOrEmpty(selfDir)
            && string.Equals(
                Path.GetFullPath(peerDir).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(selfDir).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }
}