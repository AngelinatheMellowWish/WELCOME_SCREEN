using System.Text.Json;
using Object1688.Shared.Ipc;
using Object1688.Shared.Text;

namespace Object1688.Tests;

/// <summary>
/// BannerRequest IPC 载荷契约测试（Main→Overlay TriggerCommand，架构 §2.3）。
/// 契约点：camelCase 属性名、枚举转字符串（wrapStrategy/align 为线上协议字符串）、
/// 全字段往返一致、缺省字段在反序列化端保持默认值。
/// </summary>
public class BannerRequestJsonTests
{
    private static BannerRequest CreateRequest() => new()
    {
        DisplayLines = new[]
        {
            new DisplayLine { Text = "第一行" },
            new DisplayLine { Text = "第二行", FontSize = 150 },
        },
        FontSize = 96,
        Color = "#FFFFFF",
        OutlineColor = "#000000",
        OutlineWidth = 4,
        Align = TextAlignment.Center,
        WrapStrategy = WrapStrategy.Wrap,
        DelaySeconds = 2,
        TargetScreen = "primary",
        TimeFormat = "HH:mm",
        FadeInMs = 400,
        HoldSeconds = 4,
        FadeOutMs = 600,
        Position = "center",
        CustomX = 0.5,
        CustomY = 0.5,
        AppName = "notepad",
        TriggerTime = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.FromHours(8)),
        PlaySound = false,
    };

    [Fact]
    public void Serialize_UsesCamelCaseAndStringEnums()
    {
        var request = CreateRequest();
        var json = JsonSerializer.Serialize(request, IpcJson.Options);

        Assert.Contains("\"displayLines\"", json);
        Assert.Contains("\"fontSize\":96", json);
        Assert.Contains("\"wrapStrategy\":\"Wrap\"", json);
        Assert.Contains("\"timeFormat\":\"HH:mm\"", json);
        Assert.Contains("\"targetScreen\":\"primary\"", json);
        Assert.Contains("\"position\":\"center\"", json);
    }

    [Fact]
    public void Serialize_DisplayLineFields_AreCamelCase()
    {
        var request = CreateRequest();
        var json = JsonSerializer.Serialize(request, IpcJson.Options);

        Assert.Contains("\"text\":\"第一行\"", json);
        Assert.Contains("\"fontSize\":150", json);
    }

    [Fact]
    public void Serialize_EnumValues_AreProtocolStrings()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[]
            {
                new DisplayLine { Text = "A", Align = TextAlignment.Left },
            },
            Align = TextAlignment.Left,
            WrapStrategy = WrapStrategy.Shrink,
        };
        var json = JsonSerializer.Serialize(request, IpcJson.Options);

        Assert.Contains("\"align\":\"Left\"", json); // 行 + 块级枚举均转字符串
        Assert.Contains("\"wrapStrategy\":\"Shrink\"", json);
    }

    [Fact]
    public void RoundTrip_EnvelopePayload_PreservesAllFields()
    {
        var request = CreateRequest();
        var payload = JsonSerializer.SerializeToElement(request, IpcJson.Options);
        var envelope = IpcEnvelope.Create(IpcMessageType.TriggerCommand, 7, payload);

        var json = JsonSerializer.Serialize(envelope, IpcJson.Options);
        var roundTrip = JsonSerializer.Deserialize<IpcEnvelope>(json, IpcJson.Options)!;
        var restored = roundTrip.GetPayload<BannerRequest>();

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.DisplayLines.Count);
        Assert.Equal("第一行", restored.DisplayLines[0].Text);
        Assert.Equal(150, restored.DisplayLines[1].FontSize);
        Assert.Equal(96, restored.FontSize);
        Assert.Equal("#FFFFFF", restored.Color);
        Assert.Equal("#000000", restored.OutlineColor);
        Assert.Equal(4, restored.OutlineWidth);
        Assert.Equal(TextAlignment.Center, restored.Align);
        Assert.Equal(WrapStrategy.Wrap, restored.WrapStrategy);
        Assert.Equal(2, restored.DelaySeconds);
        Assert.Equal("primary", restored.TargetScreen);
        Assert.Equal("HH:mm", restored.TimeFormat);
        Assert.Equal(400, restored.FadeInMs);
        Assert.Equal(4, restored.HoldSeconds);
        Assert.Equal(600, restored.FadeOutMs);
        Assert.Equal("center", restored.Position);
        Assert.Equal(0.5, restored.CustomX);
        Assert.Equal(0.5, restored.CustomY);
        Assert.Equal("notepad", restored.AppName);
        Assert.Equal(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.FromHours(8)), restored.TriggerTime);
        Assert.False(restored.PlaySound);
    }

    [Fact]
    public void Deserialize_MissingOptionalFields_KeepDefaults()
    {
        // Main 只发必需字段（displayLines）时，Overlay 端其余字段取默认值
        const string json = """{"displayLines":[{"text":"你好"}]}""";
        var request = JsonSerializer.Deserialize<BannerRequest>(json, IpcJson.Options);

        Assert.NotNull(request);
        Assert.Equal(96, request!.FontSize);
        Assert.Equal("#FFFFFF", request.Color);
        Assert.Equal("#000000", request.OutlineColor);
        Assert.Equal(0, request.OutlineWidth);
        Assert.Equal(TextAlignment.Center, request.Align);
        Assert.Equal(WrapStrategy.Wrap, request.WrapStrategy);
        Assert.Equal(0, request.DelaySeconds);
        Assert.Equal("primary", request.TargetScreen);
        Assert.Equal("auto", request.TimeFormat);
        Assert.Equal(400, request.FadeInMs);
        Assert.Equal(4, request.HoldSeconds);
        Assert.Equal(600, request.FadeOutMs);
        Assert.Equal("center", request.Position);
        Assert.False(request.PlaySound);
    }

    [Fact]
    public void Deserialize_MissingDisplayLines_Fails()
    {
        // displayLines 为必需字段：缺失应反序列化失败（空类型保护）
        const string json = """{"fontSize":96}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BannerRequest>(json, IpcJson.Options));
    }

    [Fact]
    public void Deserialize_UnknownAlignValue_Fails()
    {
        // 未知枚举值 → JsonException（协议字符串不匹配即报错，防静默错配）
        const string json = """{"displayLines":[{"text":"A"}],"align":"Sideways"}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BannerRequest>(json, IpcJson.Options));
    }
}