using System.Text.Json;
using Object1688.Shared.Ipc;

namespace Object1688.Tests;

/// <summary>
/// IPC 信封/JSON 序列化/协议常量测试（架构 §2.3 协议定版）。
/// 契约点：camelCase 属性名、枚举转字符串（枚举名即线上协议字符串）、null 载荷省略、
/// 版本号、超时/管道名常量锁定。
/// </summary>
public class IpcEnvelopeJsonTests
{
    // ===== IpcEnvelope.Create =====

    [Fact]
    public void Create_SetsProtocolVersionAndSeq()
    {
        var envelope = IpcEnvelope.Create(IpcMessageType.Heartbeat, 42);
        Assert.Equal(IpcProtocol.Version, envelope.ProtocolVersion);
        Assert.Equal(42, envelope.Seq);
        Assert.Equal(IpcMessageType.Heartbeat, envelope.Type);
        Assert.Null(envelope.Payload);
    }

    [Fact]
    public void Create_TimestampIsRecentUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var envelope = IpcEnvelope.Create(IpcMessageType.Heartbeat, 0);
        var after = DateTimeOffset.UtcNow;
        Assert.InRange(envelope.Timestamp, before, after);
    }

    [Fact]
    public void Create_WithPayload_RetainsElement()
    {
        var payload = JsonSerializer.SerializeToElement(new { Foo = "bar" }, IpcJson.Options);
        var envelope = IpcEnvelope.Create(IpcMessageType.StatusReport, 1, payload);
        Assert.NotNull(envelope.Payload);
        Assert.Equal("bar", envelope.Payload!.Value.GetProperty("foo").GetString());
    }

    // ===== 序列化契约 =====

    [Fact]
    public void Serialize_UsesCamelCaseAndStringEnum()
    {
        var envelope = IpcEnvelope.Create(IpcMessageType.Heartbeat, 7);
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        Assert.Contains("\"protocolVersion\":1", json);
        Assert.Contains("\"type\":\"Heartbeat\"", json); // 枚举名即线上协议字符串
        Assert.Contains("\"seq\":7", json);
        Assert.Contains("\"timestamp\":", json);
    }

    [Fact]
    public void Serialize_DropsNullPayload()
    {
        var envelope = IpcEnvelope.Create(IpcMessageType.Shutdown, 0);
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        Assert.DoesNotContain("payload", json);
    }

    [Fact]
    public void RoundTrip_PreservesEnvelope()
    {
        var payload = JsonSerializer.SerializeToElement(new { N = 5 }, IpcJson.Options);
        var envelope = IpcEnvelope.Create(IpcMessageType.ConfigChanged, 99, payload);
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var roundTrip = JsonSerializer.Deserialize<IpcEnvelope>(json, IpcJson.Options)!;
        Assert.Equal(IpcProtocol.Version, roundTrip.ProtocolVersion);
        Assert.Equal(IpcMessageType.ConfigChanged, roundTrip.Type);
        Assert.Equal(99L, roundTrip.Seq);
        Assert.Equal(5, roundTrip.Payload!.Value.GetProperty("n").GetInt32());
    }

    [Fact]
    public void RoundTrip_PayloadTyped_Handshake()
    {
        var handshake = new IpcHandshake
        {
            Role = IpcRole.Monitor,
            Pid = 1234,
            ExecutablePath = @"C:\app\Object1688.Monitor.exe",
        };
        var envelope = IpcEnvelope.Create(
            IpcMessageType.Handshake,
            1,
            JsonSerializer.SerializeToElement(handshake, IpcJson.Options));
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var roundTrip = JsonSerializer.Deserialize<IpcEnvelope>(json, IpcJson.Options)!;
        var typed = roundTrip.GetPayload<IpcHandshake>();
        Assert.NotNull(typed);
        Assert.Equal(IpcRole.Monitor, typed!.Role);
        Assert.Equal(1234, typed.Pid);
        Assert.Equal(@"C:\app\Object1688.Monitor.exe", typed.ExecutablePath);
    }

    [Fact]
    public void RoundTrip_PayloadNull_ReturnsDefault()
    {
        var envelope = IpcEnvelope.Create(IpcMessageType.HandshakeAck, 1);
        Assert.Null(envelope.GetPayload<IpcHandshake>());
    }

    [Fact]
    public void RoundTrip_PayloadTyped_TestPlay_WithBannerRequest()
    {
        // ConfigUI"测试显示"（架构 §5.6 F-23）：TestPlay 载荷为 BannerRequest，Main 校验后转 TriggerCommand
        var request = new Object1688.Shared.Text.BannerRequest
        {
            DisplayLines = new[] { new Object1688.Shared.Text.DisplayLine { Text = "测试大字" } },
            FontSize = 96,
            Color = "#FFFFFF",
            HoldSeconds = 3,
            TargetScreen = "primary",
        };
        var envelope = IpcEnvelope.Create(
            IpcMessageType.TestPlay,
            1,
            JsonSerializer.SerializeToElement(request, IpcJson.Options));
        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        Assert.Contains("\"type\":\"TestPlay\"", json);
        var roundTrip = JsonSerializer.Deserialize<IpcEnvelope>(json, IpcJson.Options)!;
        var typed = roundTrip.GetPayload<Object1688.Shared.Text.BannerRequest>();
        Assert.NotNull(typed);
        Assert.Equal("测试大字", typed!.DisplayLines[0].Text);
        Assert.Equal(96, typed.FontSize);
        Assert.Equal("primary", typed.TargetScreen);
    }

    [Fact]
    public void AllMessageTypes_SerializeToTheirEnumNames()
    {
        foreach (var type in Enum.GetValues<IpcMessageType>())
        {
            var envelope = IpcEnvelope.Create(type, 0);
            var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
            Assert.Contains($"\"type\":\"{type}\"", json); // 枚举名 = 协议字符串，禁改名
        }
    }

    // ===== 协议常量锁定 =====

    [Fact]
    public void ProtocolVersion_IsOne()
    {
        Assert.Equal(1, IpcProtocol.Version);
    }

    [Fact]
    public void PipeNames_AndPrefix_AreCanonical()
    {
        Assert.Equal("Object1688.main", IpcProtocol.MainControlPipe);
        Assert.Equal("Object1688.logging", IpcProtocol.LoggingPipe);
        Assert.Equal(@"\\.\pipe\", IpcProtocol.PipePrefix);
    }

    [Fact]
    public void TimingConstants_AreLocked()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), IpcProtocol.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromSeconds(6), IpcProtocol.HeartbeatTimeout);
        Assert.Equal(TimeSpan.FromSeconds(3), IpcProtocol.ShutdownGraceTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), IpcProtocol.SessionEndingFlushTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), IpcProtocol.HandshakeTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), IpcProtocol.RestartBackoffCap);
    }

    [Fact]
    public void IpcRole_NamesAreCanonical()
    {
        Assert.Equal(
            new[] { "Main", "Logging", "Overlay", "Monitor", "ConfigUI" },
            Enum.GetNames<IpcRole>());
    }

    [Fact]
    public void ShutdownReason_NamesAreCanonical()
    {
        Assert.Equal(
            new[] { "UserExit", "UserExitNoAutostart", "SessionEnding" },
            Enum.GetNames<ShutdownReason>());
    }
}