using System.Text.Json;
using Object1688.Shared.Ipc;
using Object1688.Shared.Monitor;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// TriggerEventReport IPC 载荷契约测试（Overlay→Main TriggerEvent，架构 §2.3/§4.4）。
/// 契约点：camelCase 属性名、枚举转字符串（matchType 为线上协议字符串）、
/// 全字段往返一致、必需字段缺失反序列化失败、缺省字段保持默认值。
/// </summary>
public class TriggerEventReportJsonTests
{
    private static TriggerEventReport CreateReport() => new()
    {
        RuleId = "r-001",
        MatchType = MatchType.WindowTitle,
        MatchValue = "记事本",
        MatchedText = "记事本 - 未命名",
        ProcessId = 4242,
        IsFullscreen = true,
        IsReappearTrigger = false,
        TriggeredAtUtc = new DateTimeOffset(2026, 9, 9, 4, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void Serialize_UsesCamelCaseAndStringEnums()
    {
        var report = CreateReport();
        var json = JsonSerializer.Serialize(report, IpcJson.Options);

        Assert.Contains("\"ruleId\":\"r-001\"", json);
        Assert.Contains("\"matchType\":\"WindowTitle\"", json); // 枚举名即线上协议字符串
        Assert.Contains("\"matchValue\":\"记事本\"", json);
        Assert.Contains("\"matchedText\":\"记事本 - 未命名\"", json);
        Assert.Contains("\"processId\":4242", json);
        Assert.Contains("\"isFullscreen\":true", json);
        Assert.Contains("\"isReappearTrigger\":false", json);
        Assert.Contains("\"triggeredAtUtc\":", json);
    }

    [Fact]
    public void Serialize_ProcessMatchType_IsString()
    {
        var report = new TriggerEventReport
        {
            RuleId = "r-002",
            MatchType = MatchType.Process,
            MatchValue = "chrome.exe",
            MatchedText = "chrome.exe",
            ProcessId = 7,
        };
        var json = JsonSerializer.Serialize(report, IpcJson.Options);
        Assert.Contains("\"matchType\":\"Process\"", json);
    }

    [Fact]
    public void RoundTrip_EnvelopePayload_PreservesAllFields()
    {
        var report = CreateReport();
        var payload = JsonSerializer.SerializeToElement(report, IpcJson.Options);
        var envelope = IpcEnvelope.Create(IpcMessageType.TriggerEvent, 7, payload);

        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var roundTrip = JsonSerializer.Deserialize<IpcEnvelope>(json, IpcJson.Options)!;
        var restored = roundTrip.GetPayload<TriggerEventReport>();

        Assert.NotNull(restored);
        Assert.Equal("r-001", restored!.RuleId);
        Assert.Equal(MatchType.WindowTitle, restored.MatchType);
        Assert.Equal("记事本", restored.MatchValue);
        Assert.Equal("记事本 - 未命名", restored.MatchedText);
        Assert.Equal(4242, restored.ProcessId);
        Assert.True(restored.IsFullscreen);
        Assert.False(restored.IsReappearTrigger);
        Assert.Equal(new DateTimeOffset(2026, 9, 9, 4, 0, 0, TimeSpan.Zero), restored.TriggeredAtUtc);
    }

    [Fact]
    public void Deserialize_MissingRequiredFields_Fails()
    {
        // ruleId/matchType/matchValue/matchedText/processId 为必需字段：缺失应反序列化失败
        const string json = """{"matchType":"Process","matchValue":"chrome.exe","matchedText":"chrome.exe","processId":1}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TriggerEventReport>(json, IpcJson.Options));
    }

    [Fact]
    public void Deserialize_MissingOptionalFields_KeepDefaults()
    {
        const string json = """
            {"ruleId":"r1","matchType":"Process","matchValue":"chrome.exe","matchedText":"chrome.exe","processId":5}
            """;
        var report = JsonSerializer.Deserialize<TriggerEventReport>(json, IpcJson.Options);

        Assert.NotNull(report);
        Assert.False(report!.IsFullscreen);
        Assert.False(report.IsReappearTrigger);
        Assert.True(report.TriggeredAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1)); // 缺省 = 反序列化时点
    }

    [Fact]
    public void Deserialize_UnknownMatchType_Fails()
    {
        const string json = """
            {"ruleId":"r1","matchType":"Registry","matchValue":"x","matchedText":"x","processId":5}
            """;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TriggerEventReport>(json, IpcJson.Options));
    }
}