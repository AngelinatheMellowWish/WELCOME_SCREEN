using System.IO.Pipes;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;

namespace Object1688.Shared.Ipc;

/// <summary>
/// 外部脚本控制接口服务端（F-76，AC-97/AC-99）。
/// 职责：监听 <see cref="ControlProtocol.PipeName"/>（仅当前用户 ACL），每个连接读取一条 JSON 请求
/// → 交由处理器分发 → 回写一条 JSON 响应 → 关闭连接。
/// 无握手（安全边界为「同用户会话」ACL）；处理器异常/非法请求均返回错误码响应，绝不终止 accept 循环。
/// </summary>
public sealed class ControlServer : IAsyncDisposable
{
    private const int MaxInstances = 254;

    private readonly Func<ControlRequest, CancellationToken, Task<ControlResponse>> _handler;
    private readonly PipeSecurity _pipeSecurity;
    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoopTask;
    private int _instancesCreated;

    /// <summary>接受循环发生未预期异常时触发（用于诊断；不会终止服务）。</summary>
    public event EventHandler<Exception>? AcceptLoopFaulted;

    /// <summary>
    /// 初始化控制服务端。
    /// </summary>
    /// <param name="handler">请求处理器（命令分发）。</param>
    /// <param name="pipeName">管道名；缺省 <see cref="ControlProtocol.PipeName"/>（测试可注入隔离名）。</param>
    public ControlServer(Func<ControlRequest, CancellationToken, Task<ControlResponse>> handler, string? pipeName = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _pipeName = string.IsNullOrWhiteSpace(pipeName) ? ControlProtocol.PipeName : pipeName;
        _pipeSecurity = PipeSecurityFactory.CreateCurrentUserOnly();
    }

    /// <summary>启动监听（幂等：重复调用直接返回）。</summary>
    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>停止监听并释放资源。</summary>
    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
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

                continue;
            }
            catch (Exception ex)
            {
                AcceptLoopFaulted?.Invoke(this, ex);
                if (stream is not null)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }

                try
                {
                    await Task.Delay(1000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            _ = Task.Run(() => HandleClientAsync(stream, ct), CancellationToken.None);
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        // 安全描述符仅在首个实例创建时生效（Windows 命名管道惯例）；后续实例传 null。
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

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };

            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            var response = await DispatchAsync(line, ct).ConfigureAwait(false);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response, IpcJson.Options)).ConfigureAwait(false);
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
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<ControlResponse> DispatchAsync(string? line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "空请求");
        }

        ControlRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ControlRequest>(line, IpcJson.Options);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Command))
        {
            return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "请求 JSON 非法或缺少 command");
        }

        if (request.ProtocolVersion != ControlProtocol.Version)
        {
            return ControlResponse.Failure(
                ErrorCodes.IpcControlCommandInvalid,
                $"协议版本不匹配（收到 {request.ProtocolVersion}，期望 {ControlProtocol.Version}）");
        }

        try
        {
            return await _handler(request, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "命令已取消");
        }
        catch (Exception ex)
        {
            return ControlResponse.Failure(ErrorCodes.GenericUnhandled, $"命令执行异常：{ex.Message}");
        }
    }
}
