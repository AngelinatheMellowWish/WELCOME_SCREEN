using System.Text;
using Object1688.Shared.Config;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// ConfigSerializer 单测（架构 §5.1，M4a 保存链路序列化契约）。
/// 契约点：缩进 JSON、camelCase 字段名、枚举转 camelCase 字符串（与 config.default.json 模板一致）、
/// UTF-8 无 BOM 写盘、AppConfig→Serialize→ConfigLoader.Parse 往返一致。
/// </summary>
public class ConfigSerializerTests
{
    private static AppConfig SampleConfig()
    {
        var config = ConfigLoader.CreateDefault();
        return new AppConfig
        {
            SchemaVersion = config.SchemaVersion,
            Language = "zh-CN",
            Global = config.Global,
            Welcome = config.Welcome,
            Manual = config.Manual,
            Rules = new[]
            {
                new RuleConfig
                {
                    RuleId = "r-901",
                    MatchType = MatchType.WindowTitle,
                    MatchValue = "*游戏*",
                    MatchMode = MatchMode.Wildcard,
                    DisplayLines = new[] { new DisplayLine { Text = "游戏时间", FontSize = 120 } },
                    WrapStrategy = WrapStrategy.Shrink,
                },
            },
            Dnd = config.Dnd,
            Sound = config.Sound,
            Autostart = config.Autostart,
            FirstRun = config.FirstRun,
        };
    }

    [Fact]
    public void Serialize_Formatting_IsIndentedCamelCaseWithLowerEnum()
    {
        // 模板真源 config.default.json 用 camelCase 字段 + 小写枚举（"exact"/"process"/"shrink"）
        var json = ConfigSerializer.Serialize(SampleConfig());

        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"rules\"", json);
        Assert.Contains("\"welcome\"", json);
        Assert.Contains("\"matchType\": \"windowTitle\"", json);
        Assert.Contains("\"matchMode\": \"wildcard\"", json);
        Assert.Contains("\"wrapStrategy\": \"shrink\"", json);
        Assert.Contains("\n", json); // 缩进多行
        // 内存字符串不含 BOM。注意：必须显式 Ordinal——xUnit 双参重载默认 CurrentCulture，
        // zh-CN 文化将 U+FEFF 视为可忽略字符（IndexOf 恒返回 0，误报 Sub-string found）
        Assert.DoesNotContain("\uFEFF", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_OmitsNullLanguage_WhenUnset()
    {
        // WhenWritingNull：Language=null 不写出
        var json = ConfigSerializer.Serialize(ConfigLoader.CreateDefault());
        Assert.DoesNotContain("language", json);
    }

    [Fact]
    public void Serialize_Utf8NoBom_WhenWrittenToFile()
    {
        var json = ConfigSerializer.Serialize(SampleConfig());
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);

        // 首字节为 '{'（0x7B）而非 EF BB BF
        Assert.Equal(0x7B, bytes[0]);
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public void RoundTrip_SerializeThenParse_PreservesAllSections()
    {
        var original = SampleConfig();
        var json = ConfigSerializer.Serialize(original);

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        var config = result.Config;
        Assert.Equal("zh-CN", config.Language);
        Assert.Equal(WrapStrategy.Shrink, Assert.Single(config.Rules).WrapStrategy);
        Assert.Equal(MatchType.WindowTitle, config.Rules[0].MatchType);
        Assert.Equal("*游戏*", config.Rules[0].MatchValue);
        Assert.Equal("游戏时间", config.Rules[0].DisplayLines[0].Text);
        Assert.Equal(original.Global.DefaultFontSize, config.Global.DefaultFontSize);
        Assert.Equal(original.Welcome.DisplayLines.Count, config.Welcome.DisplayLines.Count);
        Assert.Equal(original.Manual.Shortcut, config.Manual.Shortcut);
    }
}