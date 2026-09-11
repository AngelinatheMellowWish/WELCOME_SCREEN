using Object1688.Shared.Config;
using Object1688.Shared.Text;
// System.IO.MatchType 与配置 MatchType 同名（SDK 隐式 using），按既有别名模式消歧
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// BannerAssembler 合并语义测试（架构 §5.7）。
/// 核心契约：「规则级字段 → 全局默认」逐项合并；规则哨兵（空串/-1）回退全局；
/// custom {x,y} 解析与越界钳制；提示音 null 跟随全局 sound 开关。
/// </summary>
public class BannerAssemblerTests
{
    private static RuleConfig CreateRule() => new()
    {
        RuleId = "r-1",
        MatchType = MatchType.Process,
        MatchValue = "chrome.exe",
        DisplayLines = new[] { new DisplayLine { Text = "浏览器已打开" } },
        Position = "top-center",
        TargetScreen = "2",
        OutlineColor = "#FF0000",
        OutlineWidth = 3,
        SoundEnabled = false,
    };

    private static GlobalConfig CreateGlobal() => new()
    {
        DefaultFontSize = 120,
        DefaultPosition = "center",
        DefaultOutlineColor = "#000000",
        DefaultOutlineWidth = 0,
        TargetScreen = "primary",
        TimeFormat = "HH:mm:ss",
    };

    private static SoundConfig CreateSound() => new() { Enabled = true, Volume = 60 };

    /// <summary>带指定位置字段的规则（RuleConfig 为 class 非 record，无 with 表达式）。</summary>
    private static RuleConfig CreateRuleWithPosition(string position) => new()
    {
        RuleId = "r-pos",
        MatchType = MatchType.Process,
        MatchValue = "chrome.exe",
        DisplayLines = new[] { new DisplayLine { Text = "浏览器已打开" } },
        Position = position,
    };

    [Fact]
    public void ComposeRule_ExplicitRuleFields_OverrideGlobalDefaults()
    {
        var request = BannerAssembler.ComposeRule(
            CreateRule(), CreateGlobal(), CreateSound(), appName: "chrome", triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.Equal("浏览器已打开", request.DisplayLines[0].Text);
        Assert.Equal("top-center", request.Position); // 规则显式值覆盖全局
        Assert.Equal("2", request.TargetScreen);
        Assert.Equal("#FF0000", request.OutlineColor);
        Assert.Equal(3, request.OutlineWidth);
        Assert.False(request.PlaySound); // 规则显式关闭覆盖全局声音开启
    }

    [Fact]
    public void ComposeRule_SentinelFields_FallBackToGlobalDefaults()
    {
        var rule = new RuleConfig
        {
            RuleId = "r-sentinel",
            MatchType = MatchType.WindowTitle,
            MatchValue = "*游戏*",
            DisplayLines = new[] { new DisplayLine { Text = "游戏时间" } },
            // 全部展示字段保持哨兵：空串 / -1（§5.7）
            Position = "",
            TargetScreen = "",
            OutlineColor = "",
            OutlineWidth = -1,
            SoundEnabled = null,
        };

        var request = BannerAssembler.ComposeRule(
            rule, CreateGlobal(), CreateSound(), appName: null, triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.Equal("center", request.Position); // 全局 DefaultPosition
        Assert.Equal("primary", request.TargetScreen); // 全局 TargetScreen
        Assert.Equal("#000000", request.OutlineColor); // 全局 DefaultOutlineColor
        Assert.Equal(0, request.OutlineWidth); // 全局 DefaultOutlineWidth
        Assert.True(request.PlaySound); // 规则 null → 跟随全局 sound.Enabled=true
    }

    [Fact]
    public void ComposeRule_BlockLevelStyle_FromGlobalDefaults()
    {
        var request = BannerAssembler.ComposeRule(
            CreateRule(), CreateGlobal(), CreateSound(), appName: "chrome", triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.Equal(120, request.FontSize); // 全局 defaultFontSize
        Assert.Equal("#FFFFFF", request.Color); // 块级纯白恒默认
        Assert.Equal(TextAlignment.Center, request.Align); // 块级对齐恒 Center
        Assert.Equal("HH:mm:ss", request.TimeFormat); // 全局 timeFormat
    }

    [Theory]
    [InlineData("custom {0.3, 0.8}", "custom", 0.3, 0.8)]
    [InlineData("custom{1,0}", "custom", 1.0, 0.0)]
    [InlineData("custom {0.25,0.75}", "custom", 0.25, 0.75)]
    [InlineData("CUSTOM {0.1, 0.9}", "custom", 0.1, 0.9)] // 大小写不敏感
    public void ComposeRule_CustomPosition_ParsesCoordinates(string position, string expected, double x, double y)
    {
        var request = BannerAssembler.ComposeRule(
            CreateRuleWithPosition(position), CreateGlobal(), CreateSound(), appName: null, triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.Equal(expected, request.Position);
        Assert.Equal(x, request.CustomX);
        Assert.Equal(y, request.CustomY);
    }

    [Theory]
    [InlineData("custom {1.5, 1.2}", 1.0, 1.0)] // 正数越界 → 钳制到 1
    [InlineData("custom {abc, 0.5}", null, null)] // 非法数字 → 坐标 null（渲染层回退 0.5）
    [InlineData("custom", null, null)] // 无坐标 → 坐标 null
    [InlineData("custom {0.5}", null, null)] // 单坐标缺逗号 → 坐标 null
    public void ComposeRule_CustomPosition_ClampsOrFallsBack(string position, double? expectedX, double? expectedY)
    {
        var request = BannerAssembler.ComposeRule(
            CreateRuleWithPosition(position), CreateGlobal(), CreateSound(), appName: null, triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.Equal("custom", request.Position);
        Assert.Equal(expectedX, request.CustomX);
        Assert.Equal(expectedY, request.CustomY);
    }

    [Fact]
    public void ComposeRule_DisplayFields_ForwardFromRule()
    {
        var rule = new RuleConfig
        {
            RuleId = "r-2",
            MatchType = MatchType.Process,
            MatchValue = "notepad.exe",
            DisplayLines = new[]
            {
                new DisplayLine { Text = "第一行", FontSize = 150 },
                new DisplayLine { Text = "第二行", Color = "#00FF00" },
            },
            WrapStrategy = WrapStrategy.Shrink,
            DelaySeconds = 5,
            HoldSeconds = 8,
        };

        var request = BannerAssembler.ComposeRule(
            rule, CreateGlobal(), CreateSound(), appName: "notepad", triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.Equal(2, request.DisplayLines.Count);
        Assert.Equal(150, request.DisplayLines[0].FontSize);
        Assert.Equal(WrapStrategy.Shrink, request.WrapStrategy);
        Assert.Equal(5, request.DelaySeconds);
        Assert.Equal(8, request.HoldSeconds);
    }

    [Fact]
    public void ComposeRule_AppNameAndTriggerTime_ForwardToPlaceholders()
    {
        var triggerTime = new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

        var request = BannerAssembler.ComposeRule(
            CreateRule(), CreateGlobal(), CreateSound(), appName: "chrome", triggerTimeUtc: triggerTime);

        Assert.Equal("chrome", request.AppName);
        Assert.Equal(triggerTime, request.TriggerTime);
    }

    [Fact]
    public void ComposeRule_SoundEnabled_True_OverridesGlobalOff()
    {
        var sound = new SoundConfig { Enabled = false };
        var rule = new RuleConfig
        {
            RuleId = "r-snd",
            MatchType = MatchType.Process,
            MatchValue = "chrome.exe",
            DisplayLines = new[] { new DisplayLine { Text = "浏览器已打开" } },
            SoundEnabled = true,
        };

        var request = BannerAssembler.ComposeRule(
            rule, CreateGlobal(), sound, appName: null, triggerTimeUtc: DateTimeOffset.UtcNow);

        Assert.True(request.PlaySound);
    }

    [Fact]
    public void ComposeWelcome_UsesGlobalForDisplayAndWelcomeForTiming()
    {
        var welcome = new WelcomeConfig
        {
            Enabled = true,
            DisplayLines = new[] { new DisplayLine { Text = "欢迎使用 Object1688" } },
            DelaySeconds = 1.5,
            HoldSeconds = 6,
            WrapStrategy = WrapStrategy.Shrink,
        };

        var request = BannerAssembler.ComposeWelcome(welcome, CreateGlobal(), CreateSound());

        Assert.Equal("欢迎使用 Object1688", request.DisplayLines[0].Text);
        Assert.Equal(120, request.FontSize); // 全局
        Assert.Equal("center", request.Position); // 全局 defaultPosition
        Assert.Equal("primary", request.TargetScreen); // 全局 targetScreen
        Assert.Equal("#000000", request.OutlineColor); // 全局
        Assert.Equal(0, request.OutlineWidth); // 全局
        Assert.Equal("HH:mm:ss", request.TimeFormat); // 全局
        Assert.Equal(1.5, request.DelaySeconds); // 欢迎节
        Assert.Equal(6, request.HoldSeconds); // 欢迎节
        Assert.Equal(WrapStrategy.Shrink, request.WrapStrategy); // 欢迎节
        Assert.Null(request.AppName);
        Assert.False(request.PlaySound); // 欢迎大字不播放提示音
    }

    [Fact]
    public void ComposeWelcome_DefaultPositionCustom_ParsesWorkspaceCoordinates()
    {
        var global = new GlobalConfig
        {
            DefaultFontSize = 120,
            DefaultPosition = "custom {0.4, 0.6}",
            DefaultOutlineColor = "#000000",
            DefaultOutlineWidth = 0,
            TargetScreen = "primary",
            TimeFormat = "auto",
        };
        var welcome = new WelcomeConfig
        {
            Enabled = true,
            DisplayLines = new[] { new DisplayLine { Text = "欢迎" } },
        };

        var request = BannerAssembler.ComposeWelcome(welcome, global, CreateSound());

        Assert.Equal("custom", request.Position);
        Assert.Equal(0.4, request.CustomX);
        Assert.Equal(0.6, request.CustomY);
    }
}