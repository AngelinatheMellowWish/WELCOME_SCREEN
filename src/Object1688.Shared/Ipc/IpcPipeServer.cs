using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Object1688.Shared.Ipc;

/// <summary>
/// IPC 命名管道服务端（架构 §2.3 协议定版）。
/// 职责：监听管道、接受连接、握手校验（AC-70 受信 PID）、按行读取 JSON 信封、广播消息。
/// 帧协议：单行 JSON（UTF-8 无 BOM，\n 分隔）。
/// </summary>
public sealed class IpcPipeServer : IAsyncDisposable
{
    // Windows 命名管道实例数合法范围 1–254，255 = PIPE_UNLIMITED_INSTANCES（系统决定）。
    // 六进程架构并发连接数远小于该值；取 254 明确上限（传 256 会抛 ArgumentOutOfRangeException）。
    private const int MaxInstances = 254;
    private readonly string _pipeName;
    private readonly Func<IpcHandshake, bool> _peerValidator;
    private readonly PipeSecurity _pipeSecurity;
    private readonly ConcurrentDictionary<int, PipeSession> _sessions = new();
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private int _nextSessionId = 1;
    private int _instancesCreated;

    /// <summary>客户端完成握手后触发。</summary>
    public event EventHandler<IpcSessionEventArgs>? ClientConnected;

    /// <summary>收到完整信封（握手消息不在此触发）。</summary>
    public event EventHandler<IpcMessageEventArgs>? MessageReceived;

    /// <summary>客户端断开/被拒后触发。</summary>
    public event EventHandler<IpcSessionEventArgs>? ClientDisconnected;

    /// <summary>接受循环发生未预期异常时触发（用于诊断/上报；不会终止服务）。</summary>
    public event EventHandler<Exception>? AcceptLoopFaulted;

    /// <summary>
    /// 初始化服务端。
    /// </summary>
    /// <param name="pipeName">管道名（不含前缀，如 "Object1688.main"）。</param>
    /// <param name="peerValidator">握手校验回调：返回 true 放行（AC-70）。</param>
    public IpcPipeServer(string pipeName, Func<IpcHandshake, bool> peerValidator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
        _peerValidator = peerValidator ?? throw new ArgumentNullException(nameof(peerValidator));
        _pipeSecurity = PipeSecurityFactory.CreateCurrentUserOnly();
    }

    /// <summary>当前已连接会话数。</summary>
    public int SessionCount => _sessions.Count;

    /// <summary>
    /// 启动监听（幂等：重复调用直接返回）。
    /// </summary>
    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>
    /// 向全部已连接会话广播信封。
    /// </summary>
    public async Task BroadcastAsync(IpcEnvelope envelope, CancellationToken ct = default)
    {
        var jsonLine = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var sessions = _sessions.Values.ToArray();
        var tasks = sessions.Select(s => s.TryWriteLineAsync(jsonLine, ct));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// 定向发送：仅向完成握手且角色匹配的会话发送信封（序列化一次，逐会话写入）。
    /// 用于 Main→Overlay 的 ConfigChanged / TriggerCommand 等单角色定向消息（§5.7/§4.4）。
    /// </summary>
    public async Task SendToRoleAsync(IpcRole role, IpcEnvelope envelope, CancellationToken ct = default)
    {
        var jsonLine = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var sessions = _sessions.Values.Where(s => s.Handshake?.Role == role).ToArray();
        var tasks = sessions.Select(s => s.TryWriteLineAsync(jsonLine, ct));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// 停止监听并断开全部会话。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        var sessions = _sessions.Values.ToArray();
        foreach (var session in sessions)
        {
            await session.CloseAsync().ConfigureAwait(false);
        }

        if (_acceptLoopTask is not null)
        {
            try
            {
                await _acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
            catch (Exception ex)
            {
                // accept 循环异常已在 AcceptLoopFaulted 上报（诊断）；此处兜底，绝不让关闭流程被吞掉
                AcceptLoopFaulted?.Invoke(this, ex);
            }
        }

        _cts?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? stream = null;
            try
            {
                stream = CreateServerStream();
                await stream.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (stream is not null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }

                break;
            }
            catch (IOException)
            {
                if (stream is not null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }

                continue; // 客户端异常断开/瞬态故障：立即重试
            }
            catch (Exception ex)
            {
                // 未预期异常（如 ACL/句柄/配额问题）：上报但绝不销毁 accept 循环（fire-and-forget 静默死亡是故障源）。
                AcceptLoopFaulted?.Invoke(this, ex);
                if (stream is not null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }

                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false); // 防抖，避免异常风暴
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            // 每个连接独立处理，不阻塞 accept 循环
            _ = Task.Run(() => HandleSessionAsync(stream, ct), CancellationToken.None);
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        // Windows 命名管道惯例：安全描述符仅在首个实例创建时生效（随首次创建固化到管道对象）。
        // 后续实例必须传 null security——重复携带描述符无意义且可能触发 ERROR_ACCESS_DENIED。
        // 注意：即便后续实例传 null，首个实例的 DACL 也须授予 FullControl 级权限，
        // 否则第二实例句柄打开时以 GENERIC_READ|GENERIC_WRITE 校验 DACL 会失败
        // （见 CreateCurrentUserOnlySecurity remarks 与 pipes 复现实验）。
        var security = Interlocked.Increment(ref _instancesCreated) == 1 ? _pipeSecurity : null;
        return NamedPipeServerStreamAcl.Create(
            _pipeName,
            PipeDirection.InOut,
            MaxInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private async Task HandleSessionAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        var sessionId = Interlocked.Increment(ref _nextSessionId);
        var session = new PipeSession(sessionId, stream);
        try
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

            // ---- 握手阶段（限时 IpcProtocol.HandshakeTimeout）----
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            handshakeCts.CancelAfter(IpcProtocol.HandshakeTimeout);
            var handshakeLine = await reader.ReadLineAsync(handshakeCts.Token).ConfigureAwait(false);
            if (handshakeLine is null)
            {
                return; // 客户端未发握手即断开
            }

            var handshakeEnvelope = Deserialize(handshakeLine);
            var handshake = handshakeEnvelope?.Payload is { } payload
                ? payload.Deserialize<IpcHandshake>(IpcJson.Options)
                : null;

            if (handshake is null
                || handshakeEnvelope!.Type != IpcMessageType.Handshake
                || handshakeEnvelope.ProtocolVersion != IpcProtocol.Version
                || !_peerValidator(handshake))
            {
                // AC-70：非本程序进程尝试伪连 → 拒绝并记 IPC-E-7003
                await WriteRejectedAsync(stream, handshake).ConfigureAwait(false);
                ClientDisconnected?.Invoke(this, new IpcSessionEventArgs(sessionId, handshake));
                return;
            }

            session.Handshake = handshake;
            session.Writer = writer;
            _sessions[sessionId] = session;

            var ack = IpcEnvelope.Create(IpcMessageType.HandshakeAck, 1, null);
            await writer.WriteLineAsync(JsonSerializer.Serialize(ack, IpcJson.Options)).ConfigureAwait(false);
            ClientConnected?.Invoke(this, new IpcSessionEventArgs(sessionId, handshake));

            // ---- 消息循环 ----
            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    break; // 客户端关闭
                }

                var envelope = Deserialize(line);
                if (envelope is null || envelope.ProtocolVersion != IpcProtocol.Version)
                {
                    continue; // 版本不匹配或非法帧：跳过
                }

                if (envelope.Type == IpcMessageType.Heartbeat)
                {
                    session.LastHeartbeat = DateTimeOffset.UtcNow;
                }

                MessageReceived?.Invoke(this, new IpcMessageEventArgs(sessionId, handshake, envelope));
            }
        }
        catch (OperationCanceledException)
        {
            // 服务端关闭
        }
        catch (IOException)
        {
            // 客户端异常断开
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            await session.CloseAsync().ConfigureAwait(false);
            ClientDisconnected?.Invoke(this, new IpcSessionEventArgs(sessionId, session.Handshake));
        }
    }

    private static IpcEnvelope? Deserialize(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<IpcEnvelope>(line, IpcJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task WriteRejectedAsync(NamedPipeServerStream stream, IpcHandshake? handshake)
    {
        try
        {
            var rejection = IpcEnvelope.Create(
                IpcMessageType.StatusReport,
                0,
                JsonSerializer.SerializeToElement(
                    new { ErrorCode = ErrorCodes.IpcPeerValidationFailed, RejectedPid = handshake?.Pid },
                    IpcJson.Options));
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rejection, IpcJson.Options) + "\n");
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // 客户端已断开，忽略
        }
    }

    /// <summary>内部会话状态。</summary>
    private sealed class PipeSession(int id, NamedPipeServerStream stream)
    {
        public int Id { get; } = id;

        public IpcHandshake? Handshake { get; set; }

        public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;

        public StreamWriter? Writer { get; set; }

        public async Task TryWriteLineAsync(string jsonLine, CancellationToken ct)
        {
            if (Writer is null)
            {
                return;
            }

            try
            {
                await Writer.WriteLineAsync(jsonLine.AsMemory(), ct).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // 客户端已断开
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // 已断开
            }
        }
    }
}

/// <summary>会话事件参数（携带已完成握手的连接方信息）。</summary>
public sealed class IpcSessionEventArgs(int sessionId, IpcHandshake? handshake) : EventArgs
{
    /// <summary>会话 ID（服务端递增分配）。</summary>
    public int SessionId { get; } = sessionId;

    /// <summary>连接方握手信息（被拒连接上可能为 null）。</summary>
    public IpcHandshake? Handshake { get; } = handshake;
}

/// <summary>消息事件参数。</summary>
public sealed class IpcMessageEventArgs(int sessionId, IpcHandshake? handshake, IpcEnvelope envelope) : EventArgs
{
    /// <summary>来源会话 ID。</summary>
    public int SessionId { get; } = sessionId;

    /// <summary>来源连接方握手信息。</summary>
    public IpcHandshake? Handshake { get; } = handshake;

    /// <summary>收到的信封。</summary>
    public IpcEnvelope Envelope { get; } = envelope;
}