using Object1688.Shared.Monitor;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;
using MatchMode = Object1688.Shared.Config.MatchMode;
using RuleConfig = Object1688.Shared.Config.RuleConfig;

namespace Object1688.Tests;

/// <summary>
/// RuleMatcher 单测（架构 §4.2/§4.5，F-10 三级匹配）。
/// 契约点：exact 桶 O(1)/contains/通配（* 与 ?）转译文/大小写语义/索引构建期排除 disabled（§4.1 匹配优先级）、
/// 全屏裁决（AC-05）、命中按配置顺序输出（同命中优先级 = rules[] 顺序，§4.2 注）。
/// </summary>
public class RuleMatcherTests
{
    private static RuleConfig Rule(string id, MatchType type, string value, MatchMode mode = MatchMode.Exact, bool enabled = true) => new()
    {
        RuleId = id,
        MatchType = type,
        MatchValue = value,
        MatchMode = mode,
        Enabled = enabled,
        DisplayLines = [new DisplayLine { Text = $"{id} 大字" }],
    };

    [Fact]
    public void Match_ProcessExactIgnoreCase_Default_CaseInsensitiveHit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "chrome.exe")]);
        var target = MatchTarget.ForProcess("Chrome.EXE", isFullscreen: false, processId: 100);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_ProcessExact_WithCaseSensitive_MismatchWhenCaseDiffers()
    {
        var rule = Rule("r1", MatchType.Process, "Chrome.exe");
        var matcher = new RuleMatcher([new RuleConfig
        {
            RuleId = rule.RuleId,
            MatchType = rule.MatchType,
            MatchValue = rule.MatchValue,
            MatchCaseSensitive = true,
            DisplayLines = rule.DisplayLines,
        }]);
        var target = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        Assert.Empty(matcher.Match(target));
    }

    [Fact]
    public void Match_ProcessExact_CaseSensitive_ExactHit()
    {
        var rule = Rule("r1", MatchType.Process, "Chrome.exe");
        var matcher = new RuleMatcher([new RuleConfig
        {
            RuleId = rule.RuleId,
            MatchType = rule.MatchType,
            MatchValue = rule.MatchValue,
            MatchCaseSensitive = true,
            DisplayLines = rule.DisplayLines,
        }]);
        var target = MatchTarget.ForProcess("Chrome.exe", isFullscreen: false, processId: 100);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_ProcessContains_SubstringHit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "chrome", MatchMode.Contains)]);
        var target = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_ProcessContains_CaseInsensitiveDefault()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "CHROME", MatchMode.Contains)]);
        var target = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_ProcessWildcard_Star_AnyCharsHit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "chrome*.exe", MatchMode.Wildcard)]);
        var hit = MatchTarget.ForProcess("chrome_2026_setup.exe", isFullscreen: false, processId: 100);
        var miss = MatchTarget.ForProcess("chromium.exe", isFullscreen: false, processId: 101);
        Assert.Contains(matcher.Match(hit), r => r.RuleId == "r1");
        Assert.Empty(matcher.Match(miss));
    }

    [Fact]
    public void Match_ProcessWildcard_Question_SingleCharHit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "win?.exe", MatchMode.Wildcard)]);
        var hit = MatchTarget.ForProcess("win1.exe", isFullscreen: false, processId: 102);
        var miss = MatchTarget.ForProcess("win12.exe", isFullscreen: false, processId: 103);
        Assert.Contains(matcher.Match(hit), r => r.RuleId == "r1");
        Assert.Empty(matcher.Match(miss));
    }

    [Fact]
    public void Match_WindowTitleExact_Hit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.WindowTitle, "王者荣耀")]);
        var target = MatchTarget.ForWindow("王者荣耀", isFullscreen: false, processId: 200);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_WindowTitleContains_Hit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.WindowTitle, "荣耀", MatchMode.Contains)]);
        var target = MatchTarget.ForWindow("王者荣耀-对局中", isFullscreen: false, processId: 200);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_WindowTitleWildcard_Hit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.WindowTitle, "王者*", MatchMode.Wildcard)]);
        var target = MatchTarget.ForWindow("王者荣耀 5v5", isFullscreen: false, processId: 200);
        Assert.Contains(matcher.Match(target), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_TypeIsolation_ProcessRuleDoesNotHitWindowTarget()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "chrome.exe")]);
        var target = MatchTarget.ForWindow("chrome.exe 标题", isFullscreen: false, processId: 100);
        Assert.Empty(matcher.Match(target));
    }

    [Fact]
    public void Match_DisabledRule_ExcludedFromIndex_NoHit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "chrome.exe", enabled: false)]);
        var target = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        Assert.Empty(matcher.Match(target));
    }

    [Fact]
    public void Match_IncludeFullscreenFalse_FullscreenTargetSuppressed()
    {
        var matcher = new RuleMatcher([new RuleConfig
        {
            RuleId = "r1",
            MatchType = MatchType.Process,
            MatchValue = "chrome.exe",
            IncludeFullscreen = false,
            DisplayLines = [new DisplayLine { Text = "r1 大字" }],
        }]);
        var fullscreen = MatchTarget.ForProcess("chrome.exe", isFullscreen: true, processId: 100);
        var normal = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        Assert.Empty(matcher.Match(fullscreen));
        Assert.Contains(matcher.Match(normal), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_IncludeFullscreenTrueDefault_FullscreenTargetHit()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "chrome.exe")]);
        var fullscreen = MatchTarget.ForProcess("chrome.exe", isFullscreen: true, processId: 100);
        Assert.Contains(matcher.Match(fullscreen), r => r.RuleId == "r1");
    }

    [Fact]
    public void Match_MultipleHits_ReturnsConfigOrder()
    {
        var matcher = new RuleMatcher(
        [
            Rule("r1", MatchType.Process, "chrome.*", MatchMode.Wildcard),
            Rule("r2", MatchType.Process, "chrome.exe"),
            Rule("r3", MatchType.Process, "chrome", MatchMode.Contains),
        ]);
        var target = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        var hits = matcher.Match(target);
        Assert.Equal(["r1", "r2", "r3"], hits.Select(static r => r.RuleId));
    }

    [Fact]
    public void Match_NoRuleHit_ReturnsEmpty()
    {
        var matcher = new RuleMatcher([Rule("r1", MatchType.Process, "firefox.exe")]);
        var target = MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100);
        Assert.Empty(matcher.Match(target));
    }
}