using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// 描边方式（架构 §3.3：方案 A 柔光 / 方案 B 精确）测试。
/// 契约点：BannerRequest 缺省 Shadow；组装「规则/手动级 → 全局」合并；空串跟随全局、非法回退 Shadow；
/// 加载器宽容解析、序列化 camelCase 往返、校验器拒绝非法取值（CFG-V-1003）。
/// </summary>
public class OutlineModeTests
{
    private static GlobalConfig Global(string mode) => new()
    {
        DefaultFontSize = 96,
        DefaultPosition = "center",
        DefaultOutlineColor = "#000000",
        DefaultOutlineWidth = 3,
        OutlineMode = mode,
    };

    private static SoundConfig Sound() => new() { Enabled = false };

    private static RuleConfig Rule(string mode) => new()
    {
        RuleId = "r-1",
        MatchType = MatchType.Process,
        MatchValue = "chrome.exe",
        DisplayLines = new[] { new DisplayLine { Text = "浏览器" } },
        OutlineColor = "#000000",
        OutlineWidth = 3,
        OutlineMode = mode,
    };

    private static AppConfig WithGlobal(GlobalConfig global)
    {
        var c = ConfigLoader.CreateDefault();
        return new AppConfig
        {
            SchemaVersion = c.SchemaVersion,
            Language = c.Language,
            Global = global,
            Welcome = c.Welcome,
            Manual = c.Manual,
            Rules = c.Rules,
            Dnd = c.Dnd,
            Sound = c.Sound,
            Autostart = c.Autostart,
            FirstRun = c.FirstRun,
        };
    }

    private static AppConfig WithRule(RuleConfig rule)
    {
        var c = ConfigLoader.CreateDefault();
        return new AppConfig
        {
            SchemaVersion = c.SchemaVersion,
            Language = c.Language,
            Global = c.Global,
            Welcome = c.Welcome,
            Manual = c.Manual,
            Rules = new[] { rule },
            Dnd = c.Dnd,
            Sound = c.Sound,
            Autostart = c.Autostart,
            FirstRun = c.FirstRun,
        };
    }

    [Fact]
    public void BannerRequest_DefaultOutlineMode_IsShadow()
        => Assert.Equal(OutlineMode.Shadow, new BannerRequest { DisplayLines = new[] { new DisplayLine { Text = "x" } } }.OutlineMode);

    [Fact]
    public void ComposeRule_ExplicitStroke_OverridesGlobalShadow()
        => Assert.Equal(
            OutlineMode.Stroke,
            BannerAssembler.ComposeRule(Rule("stroke"), Global("shadow"), Sound(), null, DateTimeOffset.UtcNow).OutlineMode);

    [Fact]
    public void ComposeRule_EmptyMode_FollowsGlobalStroke()
        => Assert.Equal(
            OutlineMode.Stroke,
            BannerAssembler.ComposeRule(Rule(""), Global("stroke"), Sound(), null, DateTimeOffset.UtcNow).OutlineMode);

    [Fact]
    public void ComposeRule_InvalidRuleAndGlobal_FallsBackShadow()
        => Assert.Equal(
            OutlineMode.Shadow,
            BannerAssembler.ComposeRule(Rule("glow"), Global("bogus"), Sound(), null, DateTimeOffset.UtcNow).OutlineMode);

    [Fact]
    public void ComposeRule_Mode_IsCaseInsensitive()
        => Assert.Equal(
            OutlineMode.Stroke,
            BannerAssembler.ComposeRule(Rule("STROKE"), Global("shadow"), Sound(), null, DateTimeOffset.UtcNow).OutlineMode);

    [Fact]
    public void ComposeWelcome_UsesGlobalMode()
        => Assert.Equal(
            OutlineMode.Stroke,
            BannerAssembler.ComposeWelcome(ConfigLoader.CreateDefault().Welcome, Global("stroke"), Sound()).OutlineMode);

    [Fact]
    public void ComposeManual_EmptyMode_FollowsGlobal()
    {
        var manual = new ManualConfig { DisplayLines = new[] { new DisplayLine { Text = "手动" } }, OutlineMode = "" };
        Assert.Equal(OutlineMode.Stroke, BannerAssembler.ComposeManual(manual, Global("stroke"), Sound()).OutlineMode);
    }

    [Fact]
    public void ComposeManual_ExplicitMode_OverridesGlobal()
    {
        var manual = new ManualConfig { DisplayLines = new[] { new DisplayLine { Text = "手动" } }, OutlineMode = "stroke" };
        Assert.Equal(OutlineMode.Stroke, BannerAssembler.ComposeManual(manual, Global("shadow"), Sound()).OutlineMode);
    }

    [Fact]
    public void ConfigLoader_Parse_RuleOutlineMode_RoundTrips()
    {
        const string json = """
        { "rules": [ { "ruleId":"r-1", "matchType":"process", "matchValue":"chrome.exe",
          "displayLines":[{"text":"浏览器"}], "outlineMode":"stroke" } ] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Issues);
        Assert.Equal("stroke", Assert.Single(result.Config.Rules).OutlineMode);
    }

    [Fact]
    public void ConfigLoader_Parse_InvalidRuleOutlineMode_SkipsRuleWithCfgV1003()
    {
        const string json = """
        { "rules": [ { "ruleId":"r-1", "matchType":"process", "matchValue":"chrome.exe",
          "displayLines":[{"text":"浏览器"}], "outlineMode":"glow" } ] }
        """;

        var result = ConfigLoader.Parse(json);

        Assert.Empty(result.Config.Rules);
        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
    }

    [Fact]
    public void ConfigSerializer_RoundTrip_PreservesOutlineModes()
    {
        var baseConfig = ConfigLoader.CreateDefault();
        var config = new AppConfig
        {
            SchemaVersion = baseConfig.SchemaVersion,
            Language = baseConfig.Language,
            Global = Global("stroke"),
            Welcome = baseConfig.Welcome,
            Manual = baseConfig.Manual,
            Rules = new[] { Rule("shadow") },
            Dnd = baseConfig.Dnd,
            Sound = baseConfig.Sound,
            Autostart = baseConfig.Autostart,
            FirstRun = baseConfig.FirstRun,
        };

        var json = ConfigSerializer.Serialize(config);

        Assert.Contains("\"outlineMode\": \"stroke\"", json);
        Assert.Contains("\"outlineMode\": \"shadow\"", json);

        var reparsed = ConfigLoader.Parse(json).Config;
        Assert.Equal("stroke", reparsed.Global.OutlineMode);
        Assert.Equal("shadow", Assert.Single(reparsed.Rules).OutlineMode);
    }

    [Fact]
    public void ConfigValidator_InvalidGlobalOutlineMode_ReportsCfgV1003()
    {
        var issues = ConfigValidator.Validate(WithGlobal(Global("bogus")));
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("outlineMode"));
    }

    [Fact]
    public void ConfigValidator_InvalidRuleOutlineMode_ReportsCfgV1003()
    {
        var issues = ConfigValidator.Validate(WithRule(Rule("glow")));
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("outlineMode"));
    }

    [Fact]
    public void ConfigValidator_ValidOutlineModes_NoIssues()
    {
        Assert.Empty(ConfigValidator.Validate(WithGlobal(Global("stroke"))));
        Assert.Empty(ConfigValidator.Validate(WithRule(Rule(""))));
        Assert.Empty(ConfigValidator.Validate(WithRule(Rule("stroke"))));
    }
}
