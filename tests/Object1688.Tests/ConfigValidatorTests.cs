using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// ConfigValidator 单测（架构 §5.3 全量校验 + §5.6 F-29 冲突/重复检测）。
/// 契约点：匹配值非空、时长边界 [0, MaxDurationSeconds]、描边色十六进制、通配语法、
/// displayLines 至少一行、勿扰时段 HH:mm、冲突检测（完全重复 → error / 范围重叠 → warning）。
/// </summary>
public class ConfigValidatorTests
{
    private static AppConfig ValidConfig()
    {
        var config = ConfigLoader.CreateDefault();
        return CloneWith(config, rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-001",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                DisplayLines = new[] { new DisplayLine { Text = "浏览器" } },
            },
        });
    }

    /// <summary>AppConfig 非 record（init-only class）→ 用该辅助重建副本并覆盖指定节。</summary>
    private static AppConfig CloneWith(
        AppConfig src,
        IReadOnlyList<RuleConfig>? rules = null,
        WelcomeConfig? welcome = null,
        DndConfig? dnd = null)
    {
        return new AppConfig
        {
            SchemaVersion = src.SchemaVersion,
            Language = src.Language,
            Global = src.Global,
            Welcome = welcome ?? src.Welcome,
            Manual = src.Manual,
            Rules = rules ?? src.Rules,
            Dnd = dnd ?? src.Dnd,
            Sound = src.Sound,
            Autostart = src.Autostart,
            FirstRun = src.FirstRun,
        };
    }

    [Fact]
    public void Validate_ValidConfig_NoIssues()
    {
        var issues = ConfigValidator.Validate(ValidConfig());
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_EmptyMatchValue_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = " ",
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("matchValue"));
    }

    [Fact]
    public void Validate_NegativeDelay_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = "x",
                DelaySeconds = -1,
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("delaySeconds"));
    }

    [Fact]
    public void Validate_ExceedMaxDuration_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = "x",
                HoldSeconds = ConfigLoader.MaxDurationSeconds + 1,
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("holdSeconds"));
    }

    [Fact]
    public void Validate_InvalidOutlineColor_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = "x",
                OutlineColor = "not-a-color",
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("描边色"));
    }

    [Fact]
    public void Validate_ValidOutlineColors_AreAccepted()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = "x",
                OutlineColor = "#0f0f0f",
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        Assert.Empty(ConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_EmptyDisplayLines_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), welcome: new WelcomeConfig { DisplayLines = [] });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("welcome.displayLines"));
    }

    [Fact]
    public void Validate_WildcardWithoutWildcardChar_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = "chrome",
                MatchMode = MatchMode.Wildcard,
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("* 或 ?"));
    }

    [Fact]
    public void Validate_WildcardWithDisallowedMetaChars_ReportsCfgV1003()
    {
        var config = CloneWith(ValidConfig(), rules: new[]
        {
            new RuleConfig
            {
                RuleId = "r-1",
                MatchType = MatchType.Process,
                MatchValue = "*win[dows]*",
                MatchMode = MatchMode.Wildcard,
                DisplayLines = new[] { new DisplayLine { Text = "x" } },
            },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigValidationFailed && i.Message.Contains("通配符语法非法"));
    }

    [Fact]
    public void Validate_DndBadTime_ReportsCfgV1009()
    {
        var config = CloneWith(ValidConfig(), dnd: new DndConfig
        {
            Schedule = new[] { new DndScheduleEntry { Days = [1], Start = "25:99", End = "18:00" } },
        });

        var issues = ConfigValidator.Validate(config);

        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigTimeFormatInvalid);
    }

    [Fact]
    public void CheckConflicts_IdenticalRules_ReportsDuplicateError()
    {
        var rules = new[]
        {
            new RuleConfig
            {
                RuleId = "r-001",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                DisplayLines = new[] { new DisplayLine { Text = "a" } },
            },
            new RuleConfig
            {
                RuleId = "r-002",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                DisplayLines = new[] { new DisplayLine { Text = "b" } },
            },
        };

        var issues = ConfigValidator.CheckConflicts(rules);

        Assert.Contains(issues, i => i.Message.Contains("完全重复"));
    }

    [Fact]
    public void CheckConflicts_CaseDiffersWithSensitivity_NotDuplicate()
    {
        var rules = new[]
        {
            new RuleConfig
            {
                RuleId = "r-001",
                MatchType = MatchType.Process,
                MatchValue = "Chrome.exe",
                MatchCaseSensitive = true,
                DisplayLines = new[] { new DisplayLine { Text = "a" } },
            },
            new RuleConfig
            {
                RuleId = "r-002",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                MatchCaseSensitive = true,
                DisplayLines = new[] { new DisplayLine { Text = "b" } },
            },
        };

        Assert.Empty(ConfigValidator.CheckConflicts(rules));
    }

    [Fact]
    public void CheckConflicts_ExactCoveredByWildcard_ReportsOverlapWarning()
    {
        var rules = new[]
        {
            new RuleConfig
            {
                RuleId = "r-001",
                MatchType = MatchType.WindowTitle,
                MatchValue = "*游戏*",
                MatchMode = MatchMode.Wildcard,
                DisplayLines = new[] { new DisplayLine { Text = "a" } },
            },
            new RuleConfig
            {
                RuleId = "r-002",
                MatchType = MatchType.WindowTitle,
                MatchValue = "某游戏",
                DisplayLines = new[] { new DisplayLine { Text = "b" } },
            },
        };

        var issues = ConfigValidator.CheckConflicts(rules);

        Assert.Contains(issues, i => i.Message.Contains("重叠"));
    }

    [Fact]
    public void CheckConflicts_UnrelatedRules_NoIssues()
    {
        var rules = new[]
        {
            new RuleConfig
            {
                RuleId = "r-001",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                DisplayLines = new[] { new DisplayLine { Text = "a" } },
            },
            new RuleConfig
            {
                RuleId = "r-002",
                MatchType = MatchType.WindowTitle,
                MatchValue = "微信",
                DisplayLines = new[] { new DisplayLine { Text = "b" } },
            },
        };

        Assert.Empty(ConfigValidator.CheckConflicts(rules));
    }

    [Fact]
    public void CheckConflicts_DisabledRuleStillDetected()
    {
        // 禁用规则保留配置（F-20），但冲突检测仍覆盖（用户重新启用时即冲突）
        var rules = new[]
        {
            new RuleConfig
            {
                RuleId = "r-001",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                DisplayLines = new[] { new DisplayLine { Text = "a" } },
            },
            new RuleConfig
            {
                RuleId = "r-002",
                MatchType = MatchType.Process,
                MatchValue = "chrome.exe",
                Enabled = false,
                DisplayLines = new[] { new DisplayLine { Text = "b" } },
            },
        };

        Assert.Contains(ConfigValidator.CheckConflicts(rules), i => i.Message.Contains("完全重复"));
    }
}