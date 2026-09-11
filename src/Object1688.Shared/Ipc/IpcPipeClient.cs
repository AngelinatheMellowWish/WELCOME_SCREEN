using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Object1688.Shared.Ipc;

/// <summary>
/// IPC 命名管道客户端（架构 §2.3 协议定版）。
/// 职责：连接服务端、发送握手、读写 JSON 信封、断线自动重连（指数退避）。
/// 帧协议：单行 JSON（UTF-8 无 BOM，\n 分隔），与服务端一致。
/// </summary>
public sealed class IpcPipeClient : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly IpcHandshake _handshake;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private NamedPipeClientStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _lifeCts;
    private Task? _receiveLoopTask;
    private long _seq;

    /// <summary>
    /// 握手确认标志：仅当服务端返回 HandshakeAck 后数据帧才可写入。
    /// 防止 SendAsync 在握手确认前抢写（服务端将首行视为握手行，
    /// 数据帧先写会被当作握手拒绝并吞掉；握手行与数据帧并发写还可能产生字节交错）。
    /// </summary>
    private volatile bool _handshakeAcked;

    /// <summary>连接建立且握手完成后触发（含重连成功）。</summary>
    public event EventHandler? Connected;

    /// <summary>收到完整信封（不含握手确认）。</summary>
    public event EventHandler<IpcEnvelope>? MessageReceived;

    /// <summary>连接断开（主动停止除外）。</summary>
    public event EventHandler? Disconnected;

    /// <summary>当前是否已连接且握手完成。</summary>
    public bool IsConnected => _stream is not null && _handshakeAcked && _stream.IsConnected;

    /// <summary>
    /// 初始化客户端。
    /// </summary>
    /// <param name="pipeName">管道名（不含前缀，如 "Object1688.main"）。</param>
    /// <param name="handshake">本进程握手信息（Role + Pid + ExecutablePath）。</param>
    public IpcPipeClient(string pipeName, IpcHandshake handshake)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
        _handshake = handshake ?? throw new ArgumentNullException(nameof(handshake));
    }

    /// <summary>
    /// 启动连接并保持接收循环（幂等；自动重连直至取消）。
    /// </summary>
    /// <param name="ct">取消即停止。</param>
    public Task RunAsync(CancellationToken ct)
    {
        if (_lifeCts is not null)
        {
            return Task.CompletedTask;
        }

        _lifeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveLoopTask = Task.Run(() => ConnectLoopAsync(_lifeCts.Token));
        return _receiveLoopTask;
    }

    /// <summary>
    /// 发送信封（自动等待连接就绪；未连接则在连接循环外直接排队）。
    /// </summary>
    public async Task SendAsync(IpcEnvelope envelope, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        while (true)
        {
            var writer = _writer;
            if (writer is not null && _handshakeAcked && _stream is { IsConnected: true })
            {
                await _writeGate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    // 再次确认写入对象仍有效
                    if (ReferenceEquals(writer, _writer) && _handshakeAcked && _stream.IsConnected)
                    {
                        await writer.WriteLineAsync(json.AsMemory(), ct).ConfigureAwait(false);
                        return;
                    }
                }
                finally
                {
                    _writeGate.Release();
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 生成并发送标准消息。
    /// </summary>
    public Task SendAsync(IpcMessageType type, object? payload, CancellationToken ct = default)
    {
        var envelope = IpcEnvelope.Create(
            type,
            Interlocked.Increment(ref _seq),
            payload is null ? null : JsonSerializer.SerializeToElement(payload, IpcJson.Options));
        return SendAsync(envelope, ct);
    }

    /// <summary>
    /// 停止连接并释放资源。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_lifeCts is null)
        {
            return;
        }

        await _lifeCts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_receiveLoopTask is not null)
            {
                await _receiveLoopTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }

        await CloseStreamAsync().ConfigureAwait(false);
        _lifeCts.Dispose();
        _writeGate.Dispose();
        _lifeCts = null;
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromMilliseconds(200);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TryConnectAsync(ct).ConfigureAwait(false);
                backoff = TimeSpan.FromMilliseconds(200); // 成功后重置退避
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                await ReceiveLoopAsync(ct).ConfigureAwait(false);
                // 接收循环自然退出 → 连接断开
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // 连接失败/中断 → 退避重连
            }
            catch (UnauthorizedAccessException)
            {
                // ACL 拒绝（仅当前用户）→ 退避重连
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            Disconnected?.Invoke(this, EventArgs.Empty);
            await Task.Delay(backoff, ct).ConfigureAwait(false);
            backoff = TimeSpan.FromMilliseconds(Math.Min((int)(backoff.TotalMilliseconds * 2), 5000));
        }
    }

    private async Task TryConnectAsync(CancellationToken ct)
    {
        // 每次连接尝试须重新握手：握手确认前数据帧一律不得写入
        _handshakeAcked = false;

        var stream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await stream.ConnectAsync(ct).ConfigureAwait(false);

        _stream?.Dispose();
        _stream = stream;
        _reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

        // 握手（写入纳入 _writeGate：与数据帧互斥，保证首行必为握手行，杜绝字节交错/乱序）
        var handshakeEnvelope = IpcEnvelope.Create(
            IpcMessageType.Handshake,
            Interlocked.Increment(ref _seq),
            JsonSerializer.SerializeToElement(_handshake, IpcJson.Options));
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(
                JsonSerializer.Serialize(handshakeEnvelope, IpcJson.Options).AsMemory(),
                ct).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }

        // 等握手确认（限时）
        using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ackCts.CancelAfter(IpcProtocol.HandshakeTimeout);
        var ackLine = await _reader.ReadLineAsync(ackCts.Token).ConfigureAwait(false);
        var ack = ackLine is null
            ? null
            : JsonSerializer.Deserialize<IpcEnvelope>(ackLine, IpcJson.Options);
        if (ack is null || ack.Type != IpcMessageType.HandshakeAck)
        {
            throw new IOException("握手被服务端拒绝。");
        }

        _handshakeAcked = true; // 握手确认完成，数据帧放行
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var reader = _reader;
        if (reader is null)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
            {
                return; // 服务端关闭
            }

            try
            {
                var envelope = JsonSerializer.Deserialize<IpcEnvelope>(line, IpcJson.Options);
                if (envelope is { Type: not IpcMessageType.HandshakeAck })
                {
                    MessageReceived?.Invoke(this, envelope);
                }
            }
            catch (JsonException)
            {
                // 非法帧：跳过
            }
        }
    }

    private async Task CloseStreamAsync()
    {
        _handshakeAcked = false; // 断流后握手状态复位（重连须重新握手）
        _writer = null;
        _reader = null;
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }
    }
}