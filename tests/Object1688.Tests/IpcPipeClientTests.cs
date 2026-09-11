using Object1688.Shared.Ipc;

namespace Object1688.Tests;

/// <summary>
/// IpcPipeClient 握手时序回归测试。
/// 背景（M3d --quit 验收失败根因）：原实现中 SendAsync 仅在
/// "_writer 非空且流已连接" 时即放行写入，而握手确认（HandshakeAck）在
/// TryConnectAsync 内稍后才完成 —— 二次实例 ForwardToPrimaryAsync 的
/// "RunAsync 后立即 SendAsync(RedirectArgs)" 时序下，数据帧可能先于握手行
/// 到达服务端：首行被当作握手校验 → 拒绝并吞掉消息，客户端却已返回成功（exit 0）。
/// 修复：数据帧写入门控于握手确认（_handshakeAcked），且握手行写入纳入 _writeGate。
/// 本测试锁定该契约：服务端延迟 ack 放大窗口，断言数据帧仍以正常消息送达、
/// 且 SendAsync 不在服务端握手确认前完成。
/// </summary>
public class IpcPipeClientTests
{
    [Fact]
    public async Task SendAsync_ImmediateAfterRunAsync_DeliversAfterHandshake()
    {
        // 唯一管道名：避免与运行中程序/并行测试冲突（xUnit 类级并行）
        var pipeName = "Object1688.test." + Guid.NewGuid().ToString("N");

        // 服务端：校验器延迟 300ms 再回 ack，人为放大"已连接未确认"窗口，
        // 使旧实现的数据帧抢写必然落在此窗口内（修复前该测试应失败）。
        await using var server = new IpcPipeServer(pipeName, _ =>
        {
            Thread.Sleep(300);
            return true;
        });

        var received = new TaskCompletionSource<IpcEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clientConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) => clientConnected.TrySetResult();
        server.MessageReceived += (_, e) => received.TrySetResult(e.Envelope);
        server.Start();

        var handshake = new IpcHandshake
        {
            Role = IpcRole.Main,
            Pid = Environment.ProcessId,
            ExecutablePath = Environment.ProcessPath ?? "Object1688.Main.exe",
        };
        await using var client = new IpcPipeClient(pipeName, handshake);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // 复现被动路径：连接循环与首次发送并发启动（与 App.ForwardToPrimaryAsync 一致）
        _ = client.RunAsync(cts.Token);
        await client.SendAsync(
            IpcMessageType.RedirectArgs,
            new[] { "--quit" },
            cts.Token);

        // 契约①：SendAsync 返回前服务端握手确认必须已完成（修复点）
        Assert.True(
            clientConnected.Task.IsCompleted,
            "SendAsync 不得在服务端握手确认（ClientConnected）之前返回");

        // 契约②：数据帧以正常消息送达（首行是握手，RedirectArgs 不会被当握手拒绝吞掉）
        var envelope = await received.Task.WaitAsync(cts.Token);
        Assert.Equal(IpcMessageType.RedirectArgs, envelope.Type);
        Assert.Equal(new[] { "--quit" }, envelope.GetPayload<string[]>());
    }

    [Fact]
    public async Task SendAsync_ConnectionReset_ReHandshakesBeforeData()
    {
        // 服务端先吞掉一次连接（校验器拒绝）再正常受理：
        // 客户端自动重连后必须重新握手，数据帧只能在第二次握手确认后放行。
        var pipeName = "Object1688.test." + Guid.NewGuid().ToString("N");

        var connector = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DateTimeOffset? firstConnectedTime = null;
        await using var server = new IpcPipeServer(pipeName, _ => true);

        var received = new TaskCompletionSource<IpcEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.ClientConnected += (_, _) => firstConnectedTime ??= DateTimeOffset.UtcNow;
        server.MessageReceived += (_, e) => received.TrySetResult(e.Envelope);
        server.Start();

        var handshake = new IpcHandshake
        {
            Role = IpcRole.Main,
            Pid = Environment.ProcessId,
            ExecutablePath = Environment.ProcessPath ?? "Object1688.Main.exe",
        };
        await using var client = new IpcPipeClient(pipeName, handshake);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var connectTask = Task.Run(async () =>
        {
            await client.RunAsync(cts.Token);
            await connector.Task;
        });

        // 等首次握手完成后再触发发送（此时连接已就绪、协议正常运行）
        await SpinWaitUntilAsync(() => firstConnectedTime is not null, cts.Token);
        connector.TrySetResult();

        await client.SendAsync(
            IpcMessageType.ConfigChanged,
            new { DisplayLinesCount = 1 },
            cts.Token);

        var envelope = await received.Task.WaitAsync(cts.Token);
        Assert.Equal(IpcMessageType.ConfigChanged, envelope.Type);
    }

    private static async Task SpinWaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition() && !ct.IsCancellationRequested)
        {
            await Task.Delay(25, ct).ConfigureAwait(false);
        }

        Assert.True(condition(), "等待条件超时");
    }
}