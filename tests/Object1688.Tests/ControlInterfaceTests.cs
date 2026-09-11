using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Object1688.Shared;
using Object1688.Shared.Ipc;

namespace Object1688.Tests;

/// <summary>
/// 外部脚本控制接口测试（F-76/F-77，AC-97/98/99）。
/// 契约点：控制管道 JSON 请求/响应往返；非法 JSON 与协议版本不匹配返回 IPC-E-7005；
/// 处理器响应原样透传；管道名可注入以隔离测试。
/// </summary>
public class ControlInterfaceTests
{
    private static string UniquePipe() => "Object1688.test.control." + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task Server_Client_RoundTrip_ReturnsHandlerResponse()
    {
        var pipe = UniquePipe();
        var server = new ControlServer(
            (req, _) => Task.FromResult(ControlResponse.Success(JsonSerializer.SerializeToElement(new { echo = req.Command }, IpcJson.Options))),
            pipe);
        server.Start();
        try
        {
            var response = await ControlClient.TrySendAsync(
                new ControlRequest { Command = ControlProtocol.Commands.Ping }, default, pipe);

            Assert.NotNull(response);
            Assert.True(response!.Ok);
            Assert.Null(response.ErrorCode);
            Assert.Equal("ping", response.Data!.Value.GetProperty("echo").GetString());
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Server_HandlerFailure_IsPassedThrough()
    {
        var pipe = UniquePipe();
        var server = new ControlServer(
            (_, _) => Task.FromResult(ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "未知命令：bogus")),
            pipe);
        server.Start();
        try
        {
            var response = await ControlClient.TrySendAsync(
                new ControlRequest { Command = "bogus" }, default, pipe);

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal(ErrorCodes.IpcControlCommandInvalid, response.ErrorCode);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Server_MalformedJson_ReturnsIpcE7005()
    {
        var pipe = UniquePipe();
        var server = new ControlServer((_, _) => Task.FromResult(ControlResponse.Success()), pipe);
        server.Start();
        try
        {
            var response = await SendRawAsync(pipe, "{ not valid json");

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal(ErrorCodes.IpcControlCommandInvalid, response.ErrorCode);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    [Fact]
    public async Task Server_ProtocolVersionMismatch_ReturnsIpcE7005()
    {
        var pipe = UniquePipe();
        var server = new ControlServer((_, _) => Task.FromResult(ControlResponse.Success()), pipe);
        server.Start();
        try
        {
            var response = await SendRawAsync(pipe, """{ "protocolVersion": 999, "command": "ping" }""");

            Assert.NotNull(response);
            Assert.False(response!.Ok);
            Assert.Equal(ErrorCodes.IpcControlCommandInvalid, response.ErrorCode);
        }
        finally
        {
            await server.DisposeAsync();
        }
    }

    private static async Task<ControlResponse?> SendRawAsync(string pipeName, string line)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await pipe.ConnectAsync(cts.Token);

        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteLineAsync(line.AsMemory(), cts.Token);
        var responseLine = await reader.ReadLineAsync(cts.Token);
        return string.IsNullOrWhiteSpace(responseLine)
            ? null
            : JsonSerializer.Deserialize<ControlResponse>(responseLine, IpcJson.Options);
    }
}
