using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Object1688.Shared.Ipc;

/// <summary>
/// 外部脚本控制接口客户端（F-77，AC-98）。
/// 连接 <see cref="ControlProtocol.PipeName"/>，发送一条 JSON 请求并读取一条 JSON 响应；
/// 主实例未运行/超时/管道不可达时返回 null（调用方以 IPC-E-7006 提示）。
/// </summary>
public static class ControlClient
{
    /// <summary>
    /// 发送控制请求（尽力而为）。
    /// </summary>
    /// <param name="request">控制请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="pipeName">管道名；缺省 <see cref="ControlProtocol.PipeName"/>（测试可注入隔离名）。</param>
    /// <returns>响应；连接失败/超时/非法响应时返回 null。</returns>
    public static async Task<ControlResponse?> TrySendAsync(ControlRequest request, CancellationToken ct = default, string? pipeName = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".", string.IsNullOrWhiteSpace(pipeName) ? ControlProtocol.PipeName : pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ControlProtocol.ConnectTimeout);

            await pipe.ConnectAsync(timeoutCts.Token).ConfigureAwait(false);

            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);

            await writer.WriteLineAsync(JsonSerializer.Serialize(request, IpcJson.Options).AsMemory(), timeoutCts.Token)
                .ConfigureAwait(false);
            var line = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(line)
                ? null
                : JsonSerializer.Deserialize<ControlResponse>(line, IpcJson.Options);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
