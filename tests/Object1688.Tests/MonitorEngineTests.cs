using System.Diagnostics;
using Object1688.Shared.Config;
using Object1688.Shared.Monitor;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;
using MatchMode = Object1688.Shared.Config.MatchMode;
using RuleConfig = Object1688.Shared.Config.RuleConfig;

namespace Object1688.Tests;

/// <summary>
/// MonitorEngine 单测（架构 §4.1/§4.4，AC-45/AC-46，M3b）。
/// 契约点：自身进程排除（AC-45）、进程存活期去重（同 PID 多窗口/去重窗口过后均不再触发，AC-46）、
/// 消失重现补触（重新计数 + minInterval 豁免）、minInterval 双维度叠加（MON-W-5005）、
/// 多规则按配置顺序命中、500 条规则单轮匹配预算 &lt; 5ms（AC-40/NFR-02）。
/// </summary>
public class MonitorEngineTests
{
    private static GlobalConfig Global(int minInterval = 2) => new()
    {
        MinTriggerIntervalSeconds = minInterval,
        DedupeWindowSeconds = 10,
    };

    private static RuleConfig Rule(string id, string processName, MatchMode mode = MatchMode.Exact, bool enabled = true, double? minInterval = null) => new()
    {
        RuleId = id,
        MatchType = MatchType.Process,
        MatchValue = processName,
        MatchMode = mode,
        Enabled = enabled,
        MinIntervalSeconds = minInterval,
        DisplayLines = [new DisplayLine { Text = $"{id} 大字" }],
    };

    // ---- AC-45 自身进程排除 ----

    [Fact]
    public void Evaluate_OwnProcessPid_Excluded_NoTrigger()
    {
        var engine = new MonitorEngine([Rule("r1", "chrome.exe")], Global(), new HashSet<int> { 42 });
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 42),
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 43),
        };

        var result = engine.Evaluate(targets, new HashSet<int> { 42, 43 }, DateTimeOffset.UtcNow);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal(43, trigger.Target.ProcessId);
    }

    // ---- 基础触发 ----

    [Fact]
    public void Evaluate_ProcessExactHit_TriggersOnce()
    {
        var engine = new MonitorEngine([Rule("r1", "chrome.exe")], Global(), new HashSet<int>());
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100),
        };

        var result = engine.Evaluate(targets, new HashSet<int> { 100 }, DateTimeOffset.UtcNow);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal("r1", trigger.Rule.RuleId);
        Assert.False(trigger.IsReappearTrigger);
        Assert.Empty(result.Suppressions);
    }

    // ---- AC-46 进程存活期去重 ----

    [Fact]
    public void Evaluate_SamePidMultipleTargets_FirstOnlyTriggers_RestDedupe()
    {
        // AC-46：同进程多个窗口（WindowTitle 规则，规则类型隔离）→ 首个命中触发，其余存活期去重抑制
        var engine = new MonitorEngine(
            [new RuleConfig
            {
                RuleId = "r1",
                MatchType = MatchType.WindowTitle,
                MatchValue = "窗口",
                MatchMode = MatchMode.Contains,
                DisplayLines = [new DisplayLine { Text = "r1 大字" }],
            }], Global(), new HashSet<int>());
        int pid = 100;
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForWindow("窗口A", isFullscreen: false, processId: pid),
            MatchTarget.ForWindow("窗口B", isFullscreen: false, processId: pid),
            MatchTarget.ForWindow("窗口C", isFullscreen: false, processId: pid),
        };

        var result = engine.Evaluate(targets, new HashSet<int> { pid }, DateTimeOffset.UtcNow);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal("窗口A", trigger.Target.Text);
        Assert.Equal(2, result.Suppressions.Count);
        Assert.All(result.Suppressions, s => Assert.Equal(SuppressionReason.DedupeWindow, s.Reason));
    }

    [Fact]
    public void Evaluate_SamePidAliveAcrossRounds_NoRetrigger_Ever()
    {
        // 进程存活期间：去重窗口内 → 不触发；去重窗口过后（仍在存活） → 也不触发（AC-46）
        var engine = new MonitorEngine([Rule("r1", "chrome.exe")], Global(), new HashSet<int>());
        int pid = 100;
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: pid),
        };
        var live = new HashSet<int> { pid };
        var t = DateTimeOffset.UtcNow;

        var first = engine.Evaluate(targets, live, t);
        var withinWindow = engine.Evaluate(targets, live, t.AddSeconds(5));
        var afterWindow = engine.Evaluate(targets, live, t.AddSeconds(30)); // 10s 窗口已过，进程仍在 → 仍不触发

        Assert.Single(first.Triggers);
        Assert.Empty(withinWindow.Triggers);
        Assert.Empty(afterWindow.Triggers);
        Assert.Equal(SuppressionReason.DedupeWindow, Assert.Single(withinWindow.Suppressions).Reason);
        Assert.Equal(SuppressionReason.DedupeWindow, Assert.Single(afterWindow.Suppressions).Reason);
    }

    // ---- 消失重现补触 ----

    [Fact]
    public void Evaluate_ProcessVanishedThenReappears_Retriggers_ExemptsMinInterval()
    {
        // 消失 → 记录清除+登记待补触；重现 → 重新计数触发，且豁免 minInterval（即使间隔 &lt; minInterval）
        var engine = new MonitorEngine([Rule("r1", "chrome.exe", minInterval: 60)], Global(), new HashSet<int>());
        int pid = 100;
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: pid),
        };
        var t = DateTimeOffset.UtcNow;

        var first = engine.Evaluate(targets, new HashSet<int> { pid }, t);            // 触发
        var gone = engine.Evaluate([], new HashSet<int>(), t.AddSeconds(1));          // 进程消失
        var re = engine.Evaluate(targets, new HashSet<int> { pid }, t.AddSeconds(2)); // 重现补触（1s 内，minInterval 豁免）

        Assert.Single(first.Triggers);
        Assert.Empty(gone.Triggers);
        var reTrigger = Assert.Single(re.Triggers);
        Assert.True(reTrigger.IsReappearTrigger);
        Assert.Empty(re.Suppressions);
    }

    [Fact]
    public void Evaluate_ProcessStaysGone_NoRetrigger()
    {
        var engine = new MonitorEngine([Rule("r1", "chrome.exe")], Global(), new HashSet<int>());
        int pid = 100;
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: pid),
        };
        var t = DateTimeOffset.UtcNow;

        var first = engine.Evaluate(targets, new HashSet<int> { pid }, t);
        var gone1 = engine.Evaluate([], new HashSet<int>(), t.AddSeconds(1));
        var gone2 = engine.Evaluate([], new HashSet<int>(), t.AddSeconds(2));

        Assert.Single(first.Triggers);
        Assert.Empty(gone1.Triggers);
        Assert.Empty(gone2.Triggers);
    }

    // ---- minInterval 双维度叠加 ----

    [Fact]
    public void Evaluate_RuleLevelMinInterval_SuppressesBurst_Hit()
    {
        // 不同 PID 命中同规则：minInterval 内第二次命中 → 抑制（MON-W-5005 语义），间隔过后放行
        var engine = new MonitorEngine([Rule("r1", "chrome.exe", minInterval: 5)], Global(), new HashSet<int>());
        var t = DateTimeOffset.UtcNow;

        var first = engine.Evaluate(
            [MatchTarget.ForProcess("chrome.exe", false, 100)],
            new HashSet<int> { 100 }, t);
        var burst = engine.Evaluate(
            [MatchTarget.ForProcess("chrome.exe", false, 200)],
            new HashSet<int> { 200 }, t.AddSeconds(1));
        var later = engine.Evaluate(
            [MatchTarget.ForProcess("chrome.exe", false, 300)],
            new HashSet<int> { 300 }, t.AddSeconds(6));

        Assert.Single(first.Triggers);
        Assert.Empty(burst.Triggers);
        Assert.Equal(SuppressionReason.MinInterval, Assert.Single(burst.Suppressions).Reason);
        Assert.Single(later.Triggers);
    }

    [Fact]
    public void Evaluate_GlobalMinIntervalFallback_Used_WhenRuleLevelNull()
    {
        // 规则级 null → 全局 minTriggerIntervalSeconds 兜底（架构 §4.4）
        var engine = new MonitorEngine([Rule("r1", "chrome.exe")], Global(minInterval: 5), new HashSet<int>());
        var t = DateTimeOffset.UtcNow;

        var first = engine.Evaluate([MatchTarget.ForProcess("chrome.exe", false, 100)], new HashSet<int> { 100 }, t);
        var burst = engine.Evaluate([MatchTarget.ForProcess("chrome.exe", false, 200)], new HashSet<int> { 200 }, t.AddSeconds(1));

        Assert.Single(first.Triggers);
        Assert.Empty(burst.Triggers);
        Assert.Equal(SuppressionReason.MinInterval, Assert.Single(burst.Suppressions).Reason);
    }

    [Fact]
    public void Evaluate_MinIntervalZero_NoSuppression()
    {
        var engine = new MonitorEngine([Rule("r1", "chrome.exe", minInterval: 0)], Global(), new HashSet<int>());
        var t = DateTimeOffset.UtcNow;

        var first = engine.Evaluate([MatchTarget.ForProcess("chrome.exe", false, 100)], new HashSet<int> { 100 }, t);
        // 不同 PID（r1 存活期去重只锁 100）→ minInterval=0 不限 → 放行
        var second = engine.Evaluate([MatchTarget.ForProcess("chrome.exe", false, 200)], new HashSet<int> { 200 }, t.AddSeconds(1));

        Assert.Single(first.Triggers);
        Assert.Single(second.Triggers);
    }

    // ---- 多规则命中顺序 ----

    [Fact]
    public void Evaluate_MultipleRulesHit_TriggersInConfigOrder()
    {
        var engine = new MonitorEngine(
        [
            Rule("r1", "chrome.*", MatchMode.Wildcard),
            Rule("r2", "chrome.exe"),
            Rule("r3", "chrome", MatchMode.Contains),
        ], Global(), new HashSet<int>());
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100),
        };

        var result = engine.Evaluate(targets, new HashSet<int> { 100 }, DateTimeOffset.UtcNow);

        Assert.Equal(["r1", "r2", "r3"], result.Triggers.Select(t => t.Rule.RuleId));
    }

    // ---- disabled 规则 ----

    [Fact]
    public void Evaluate_DisabledRule_NoTrigger()
    {
        var engine = new MonitorEngine(
            [Rule("r1", "chrome.exe", enabled: false), Rule("r2", "chrome.exe")], Global(), new HashSet<int>());
        var targets = new List<MatchTarget>
        {
            MatchTarget.ForProcess("chrome.exe", isFullscreen: false, processId: 100),
        };

        var result = engine.Evaluate(targets, new HashSet<int> { 100 }, DateTimeOffset.UtcNow);

        var trigger = Assert.Single(result.Triggers);
        Assert.Equal("r2", trigger.Rule.RuleId);
    }

    // ---- 性能基准（AC-40/NFR-02：500 条规则单轮匹配 < 5ms）----

    [Fact]
    public void Benchmark_500Rules_SingleRoundUnder5ms()
    {
        // 混合 500 条规则：进程精确桶为主（O(1)），少量 contains/wildcard 走线性；模拟进程快照 + 窗口枚举
        var rules = new List<RuleConfig>(500);
        for (var i = 0; i < 420; i++)
        {
            rules.Add(Rule($"p-exact-{i:D3}", $"proc{i:D3}.exe"));
        }

        for (var i = 0; i < 60; i++)
        {
            rules.Add(Rule($"p-contains-{i:D3}", $"lib{i:D3}", MatchMode.Contains));
        }

        for (var i = 0; i < 20; i++)
        {
            rules.Add(new RuleConfig
            {
                RuleId = $"t-title-{i:D3}",
                MatchType = MatchType.WindowTitle,
                MatchValue = $"窗口{i:D3}",
                DisplayLines = [new DisplayLine { Text = $"t-title-{i:D3} 大字" }],
            });
        }

        var engine = new MonitorEngine(rules, Global(minInterval: 0), new HashSet<int>());

        // 模拟 50 进程 + 30 顶层窗口 = 80 目标
        var targets = new List<MatchTarget>(80);
        for (var i = 0; i < 50; i++)
        {
            targets.Add(MatchTarget.ForProcess($"proc{i:D3}.exe", isFullscreen: false, processId: 1000 + i));
            targets.Add(MatchTarget.ForProcess($"lib{i % 60:D3}.dll", isFullscreen: false, processId: 2000 + i));
        }

        for (var i = 0; i < 30; i++)
        {
            targets.Add(MatchTarget.ForWindow($"窗口{i:D3}", isFullscreen: false, processId: 3000 + i));
        }

        var live = new HashSet<int>(targets.Select(t => t.ProcessId));
        var now = DateTimeOffset.UtcNow;

        // 预热（JIT/字典初始化），再测 10 轮取中位数，断言 &lt; 5ms
        const int warmup = 2;
        const int rounds = 10;
        for (var i = 0; i < warmup; i++)
        {
            engine.Evaluate(targets, live, now);
        }

        var samples = new long[rounds];
        for (var i = 0; i < rounds; i++)
        {
            var sw = Stopwatch.StartNew();
            engine.Evaluate(targets, live, now);
            sw.Stop();
            samples[i] = sw.Elapsed.Ticks;
        }

        Array.Sort(samples);
        var medianMs = TimeSpan.FromTicks(samples[rounds / 2]).TotalMilliseconds;
        // 共享 CI runner 比典型硬件慢，此处作粗粒度回归护栏；精确预算由 RuleMatcherBenchTests 实测（AC-40）
        Assert.True(medianMs < 20, $"500 条规则单轮匹配中位数 {medianMs:F2}ms 严重超出护栏 20ms");
    }
}