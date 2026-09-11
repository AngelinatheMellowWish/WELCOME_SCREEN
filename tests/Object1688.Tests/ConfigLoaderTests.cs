using System.Text.Json;
using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Ipc;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// ConfigLoader 单测（架构 §5.1/§5.3/§5.7，F-22）。
/// 契约点：宽容加载（单条规则非法 CFG-V-1003 跳过其余保留）、displayText → displayLines 兼容映射、
/// matchMode/enabled 缺省（exact/true）、schemaVersion 迁移（CFG-W-1008）、
/// 缺省回退链（缺失 CFG-W-1002 / 读取失败 CFG-E-1001 / 损坏 CFG-V-1003）。
/// </summary>
public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "Object1688-Tests-" + Guid.NewGuid().ToString("N"));

    public ConfigLoaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不影响测试结论
        }
    }

    private string WriteTempFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_ValidFullConfig_MapsAllSections()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "language": "zh-CN",
          "global": { "defaultFontSize": 128, "monitorPollIntervalMs": 2000, "dedupeWindowSeconds": 15 },
          "welcome": { "enabled": false, "displayLines": [{ "text": "Hi", "fontSize": 72 }], "holdSeconds": 6 },
          "manual": { "displayLines": [{ "text": "Go" }], "shortcut": null },
          "rules": [
            {
              "ruleId": "r-001",
              "matchType": "process",
              "matchValue": "chrome.exe",
              "matchMode": "exact",
              "enabled": true,
              "matchCaseSensitive": false,
              "includeFullscreen": true,
              "targetScreen": "primary",
              "displayLines": [{ "text": "浏览器", "fontSize": 96 }],
              "wrapStrategy": "shrink",
              "delaySeconds": 2,
              "holdSeconds": 4,
              "position": "center",
              "outlineColor": "#000000",
              "outlineWidth": 0
            }
          ],
          "dnd": { "paused": true, "scheduleEnabled": true, "schedule": [{ "days": [1, 5], "start": "09:00", "end": "18:00" }] },
          "sound": { "enabled": true, "source": "custom", "customPath": "C:\\wav\\note.wav", "volume": 60 },
          "autostart": false,
          "firstRun": false
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        Assert.Equal(1, result.Config.SchemaVersion);
        Assert.Equal("zh-CN", result.Config.Language);
        Assert.Equal(128, result.Config.Global.DefaultFontSize);
        Assert.Equal(2000, result.Config.Global.MonitorPollIntervalMs);
        Assert.Equal(15, result.Config.Global.DedupeWindowSeconds);
        Assert.False(result.Config.Welcome.Enabled);
        Assert.Equal("Hi", result.Config.Welcome.DisplayLines[0].Text);
        Assert.Equal(6, result.Config.Welcome.HoldSeconds);
        Assert.Equal("Go", result.Config.Manual.DisplayLines[0].Text);
        Assert.Null(result.Config.Manual.Shortcut);

        Assert.Single(result.Config.Rules);
        var rule = result.Config.Rules[0];
        Assert.Equal("r-001", rule.RuleId);
        Assert.Equal(MatchType.Process, rule.MatchType);
        Assert.Equal("chrome.exe", rule.MatchValue);
        Assert.Equal(MatchMode.Exact, rule.MatchMode);
        Assert.True(rule.Enabled);
        Assert.False(rule.MatchCaseSensitive);
        Assert.True(rule.IncludeFullscreen);
        Assert.Equal("primary", rule.TargetScreen);
        Assert.Equal("浏览器", rule.DisplayLines[0].Text);
        Assert.Equal(WrapStrategy.Shrink, rule.WrapStrategy);
        Assert.Equal(2, rule.DelaySeconds);
        Assert.Equal(4, rule.HoldSeconds);
        Assert.Equal("center", rule.Position);
        Assert.Equal("#000000", rule.OutlineColor);

        Assert.True(result.Config.Dnd.Paused);
        Assert.True(result.Config.Dnd.ScheduleEnabled);
        Assert.Single(result.Config.Dnd.Schedule);
        Assert.Equal(new[] { 1, 5 }, result.Config.Dnd.Schedule[0].Days);
        Assert.Equal("09:00", result.Config.Dnd.Schedule[0].Start);
        Assert.Equal("18:00", result.Config.Dnd.Schedule[0].End);
        Assert.True(result.Config.Sound.Enabled);
        Assert.Equal("custom", result.Config.Sound.Source);
        Assert.Equal(60, result.Config.Sound.Volume);
        Assert.False(result.Config.Autostart);
        Assert.False(result.Config.FirstRun);
    }

    [Fact]
    public void Parse_LegacyDisplayText_MapsToSingleDisplayLine()
    {
        // 旧格式（§4.2 注 / 开发规范 §9.3）：单字段 displayText/fontSize → 单元素 displayLines
        const string json = """
        {
          "rules": [
            {
              "ruleId": "r-old",
              "matchType": "process",
              "matchValue": "notepad.exe",
              "displayText": "记事本",
              "fontSize": 88
            }
          ]
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        var rule = Assert.Single(result.Config.Rules);
        Assert.Equal("记事本", rule.DisplayLines[0].Text);
        Assert.Equal(88, rule.DisplayLines[0].FontSize);
    }

    [Fact]
    public void Parse_MissingMatchModeAndEnabled_DefaultsExactAndTrue()
    {
        const string json = """
        {
          "rules": [{ "ruleId": "r-1", "matchType": "windowTitle", "matchValue": "微信", "displayLines": [{ "text": "微信" }] }]
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        var rule = Assert.Single(result.Config.Rules);
        Assert.Equal(MatchMode.Exact, rule.MatchMode);
        Assert.True(rule.Enabled);
        Assert.Null(rule.MinIntervalSeconds);
        Assert.Null(rule.SoundEnabled);
        Assert.Equal(2, rule.DelaySeconds); // §5.7 缺省
    }

    [Fact]
    public void Parse_RuleWithoutDisplayFields_PreservesSentinels()
    {
        // §5.7：规则未显式配置的展示字段保留哨兵（空串/-1），由 BannerAssembler 合并全局默认
        const string json = """
        {
          "rules": [{ "ruleId": "r-1", "matchType": "windowTitle", "matchValue": "微信", "displayLines": [{ "text": "微信" }] }]
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        var rule = Assert.Single(result.Config.Rules);
        Assert.Equal(string.Empty, rule.Position);
        Assert.Equal(string.Empty, rule.TargetScreen);
        Assert.Equal(string.Empty, rule.OutlineColor);
        Assert.Equal(-1, rule.OutlineWidth);
    }

    [Fact]
    public void Parse_ExplicitEmptyOutlineColor_IsAllowedAndPreserved()
    {
        // §5.7：outlineColor=""（哨兵）合法，不触发 hex 校验，由合并层回退全局
        const string json = """
        {
          "rules": [{ "ruleId": "r-1", "matchType": "windowTitle", "matchValue": "微信", "outlineColor": "", "displayLines": [{ "text": "微信" }] }]
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        var rule = Assert.Single(result.Config.Rules);
        Assert.Equal(string.Empty, rule.OutlineColor);
    }

    [Fact]
    public void Parse_InvalidRule_ReportsCfgV1003AndSkipsButKeepsOthers()
    {
        const string json = """
        {
          "rules": [
            { "ruleId": "r-bad", "matchType": "process", "matchValue": "" },
            { "ruleId": "r-good", "matchType": "process", "matchValue": "chrome.exe", "displayLines": [{ "text": "浏览器" }] }
          ]
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        var rule = Assert.Single(result.Config.Rules); // 非法规则被跳过，合法保留
        Assert.Equal("r-good", rule.RuleId);
    }

    [Fact]
    public void Parse_InvalidMatchType_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "executable", "matchValue": "x.exe" }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_InvalidMatchMode_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "x", "matchMode": "regex" }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_WildcardWithDisallowedChars_ReportsCfgV1003AndSkips()
    {
        // 通配语法校验（§5.3）：含正则元字符非法
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "windowTitle", "matchValue": "*win[dows]*", "matchMode": "wildcard" }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_WildcardWithoutWildcardChar_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "chrome", "matchMode": "wildcard" }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_WildcardValid_AcceptsStarAndQuestionMark()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "code*", "matchMode": "wildcard", "displayLines": [{ "text": "代码" }] }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        Assert.Equal(MatchMode.Wildcard, Assert.Single(result.Config.Rules).MatchMode);
    }

    [Fact]
    public void Parse_NegativeDelay_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "x", "delaySeconds": -1 }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_ExceedMaxDelay_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "x", "delaySeconds": 90000 }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_InvalidOutlineColor_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "x", "outlineColor": "not-a-color" }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_MissingRuleId_ReportsCfgV1003AndSkips()
    {
        const string json = """
        { "rules": [{ "matchType": "process", "matchValue": "x" }] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Parse_OldSchemaVersion_ReportsCfgW1008Migration()
    {
        const string json = """
        { "schemaVersion": 0, "rules": [] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigSchemaMigrated);
        Assert.Equal(ConfigLoader.CurrentSchemaVersion, result.Config.SchemaVersion);
    }

    [Fact]
    public void Parse_FutureSchemaVersion_FallsBackToDefaults()
    {
        const string json = """
        { "schemaVersion": 99, "rules": [] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules); // 回退内置默认，示例规则不包含
    }

    [Fact]
    public void Parse_InvalidJson_FallsBackToDefaults()
    {
        var result = ConfigLoader.Parse("{ not valid json !!!");

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
        Assert.Empty(result.Config.Rules);
        Assert.Equal(96, result.Config.Global.DefaultFontSize);
    }

    [Fact]
    public void Parse_MissingSections_UseBuiltInDefaults()
    {
        var result = ConfigLoader.Parse("{}");

        Assert.Empty(result.Issues);
        Assert.Equal(96, result.Config.Global.DefaultFontSize);
        Assert.Equal("欢迎", result.Config.Welcome.DisplayLines[0].Text);
        Assert.Equal("Object1688", result.Config.Manual.DisplayLines[0].Text);
        Assert.True(result.Config.Autostart);
        Assert.True(result.Config.FirstRun);
        Assert.Equal(1500, result.Config.Global.MonitorPollIntervalMs);
    }

    [Fact]
    public void Load_MissingFile_ReportsCfgW1002FallsBackToTemplate()
    {
        var missing = Path.Combine(_tempDir, "missing.json");
        var template = WriteTempFile("template.json", """{ "language": "en-US" }""");

        var result = ConfigLoader.Load(missing, template);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigDefaulted);
        Assert.Equal("en-US", result.Config.Language); // 模板生效
    }

    [Fact]
    public void Load_MissingFileAndTemplate_ReportsCfgW1002AndBuiltInDefaults()
    {
        var missing = Path.Combine(_tempDir, "missing.json");
        var missingTemplate = Path.Combine(_tempDir, "no-template.json");

        var result = ConfigLoader.Load(missing, missingTemplate);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigDefaulted);
        Assert.Equal(96, result.Config.Global.DefaultFontSize);
    }

    [Fact]
    public void Load_ReadFailure_ReportsCfgE1001()
    {
        // 独占锁定文件 → File.ReadAllText 抛 IOException（共享冲突）
        var path = WriteTempFile("locked.json", "{}");
        using var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = ConfigLoader.Load(path);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigReadFailed);
        Assert.Empty(result.Config.Rules);
    }

    [Fact]
    public void Load_ValidFile_ParsesWithoutIssues()
    {
        var path = WriteTempFile("config.json", """
        { "rules": [{ "ruleId": "r-1", "matchType": "process", "matchValue": "chrome.exe", "displayLines": [{ "text": "浏览器" }] }] }
        """);

        var result = ConfigLoader.Load(path);

        Assert.Empty(result.Issues);
        Assert.Equal("chrome.exe", Assert.Single(result.Config.Rules).MatchValue);
    }

    [Fact]
    public void CreateDefault_MatchesTemplateDefaults()
    {
        var config = ConfigLoader.CreateDefault();

        Assert.Equal(1, config.SchemaVersion);
        Assert.Null(config.Language);
        Assert.Equal(96, config.Global.DefaultFontSize);
        Assert.Equal("center", config.Global.DefaultPosition);
        Assert.Equal("#000000", config.Global.DefaultOutlineColor);
        Assert.Equal(1500, config.Global.MonitorPollIntervalMs);
        Assert.Equal(10, config.Global.DedupeWindowSeconds);
        Assert.Equal(2, config.Global.MinTriggerIntervalSeconds);
        Assert.Equal("INFO", config.Global.LogLevel);
        Assert.Equal("per-monitor-v2", config.Global.DpiAwareness);
        Assert.Equal(2, config.Welcome.DisplayLines.Count);
        Assert.Equal(4, config.Welcome.HoldSeconds);
        Assert.Equal("Alt+F", config.Manual.Shortcut);
        Assert.Empty(config.Rules);
        Assert.False(config.Dnd.Paused);
        Assert.False(config.Sound.Enabled);
        Assert.Equal(80, config.Sound.Volume);
        Assert.True(config.Autostart);
        Assert.True(config.FirstRun);
        Assert.NotNull(config.UiState);
        Assert.Equal(900, config.UiState!.ConfigWin!.Width);
        Assert.Equal("rules", config.UiState.ConfigWin.Tab);
        Assert.Equal("60s", config.UiState.PerfWin!.TimeWindow);
    }

    [Fact]
    public void Parse_UiStateSection_MapsWindowLayouts()
    {
        const string json = """
        {
          "uiState": {
            "configWin": { "x": 10, "y": 20, "width": 800, "height": 600, "splitter": 300, "tab": "welcome" },
            "perfWin": { "width": 700, "height": 500, "timeWindow": "5min" }
          }
        }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        Assert.NotNull(result.Config.UiState);
        Assert.Equal(10, result.Config.UiState!.ConfigWin!.X);
        Assert.Equal(20, result.Config.UiState.ConfigWin.Y);
        Assert.Equal(800, result.Config.UiState.ConfigWin.Width);
        Assert.Equal(600, result.Config.UiState.ConfigWin.Height);
        Assert.Equal(300, result.Config.UiState.ConfigWin.Splitter);
        Assert.Equal("welcome", result.Config.UiState.ConfigWin.Tab);
        Assert.Equal(700, result.Config.UiState.PerfWin!.Width);
        Assert.Equal("5min", result.Config.UiState.PerfWin.TimeWindow);
    }

    [Fact]
    public void Parse_EmptyJson_UiStateFallsBackToBuiltInDefaults()
    {
        var result = ConfigLoader.Parse("{}");

        Assert.NotNull(result.Config.UiState);
        Assert.Equal(900, result.Config.UiState!.ConfigWin!.Width);
        Assert.Equal("overview", result.Config.UiState.PerfWin!.Tab);
    }

    [Fact]
    public void RoundTrip_SerializeThenParse_PreservesConfig()
    {
        var original = ConfigLoader.CreateDefault();
        var json = JsonSerializer.Serialize(original, IpcJson.Options);

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        var config = result.Config;
        Assert.Equal(original.Global.DefaultFontSize, config.Global.DefaultFontSize);
        Assert.Equal(original.Welcome.DisplayLines.Count, config.Welcome.DisplayLines.Count);
        Assert.Equal(original.Welcome.DisplayLines[0].Text, config.Welcome.DisplayLines[0].Text);
        Assert.Equal(original.Manual.Shortcut, config.Manual.Shortcut);
        Assert.Equal(original.Sound.Volume, config.Sound.Volume);
        Assert.Equal(original.Autostart, config.Autostart);
    }

    [Fact]
    public void Serialize_ConfigModels_UseCamelCaseAndStringEnums()
    {
        // 配置契约（Main → Overlay ConfigChanged 广播走同一 IpcJson.Options）
        var rule = new RuleConfig
        {
            RuleId = "r-1",
            MatchType = MatchType.WindowTitle,
            MatchValue = "*游戏*",
            MatchMode = MatchMode.Wildcard,
            DisplayLines = new[] { new DisplayLine { Text = "游戏时间", FontSize = 120 } },
            WrapStrategy = WrapStrategy.Shrink,
        };
        var json = JsonSerializer.Serialize(rule, IpcJson.Options);

        Assert.Contains("\"ruleId\":\"r-1\"", json);
        Assert.Contains("\"matchType\":\"WindowTitle\"", json);
        Assert.Contains("\"matchMode\":\"Wildcard\"", json);
        Assert.Contains("\"wrapStrategy\":\"Shrink\"", json);
        Assert.Contains("\"displayLines\"", json);
        Assert.DoesNotContain("\"fontSize\":0", json.Replace("\"fontSize\":120", "")); // WhenWritingNull 不写空串颜色等
    }
}