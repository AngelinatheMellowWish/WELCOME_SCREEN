using System.Diagnostics;
using Object1688.Shared.Config;
using Object1688.Shared.Monitor;
using Object1688.Shared.Text;
using MatchMode = Object1688.Shared.Config.MatchMode;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// 500 条规则匹配性能基准（架构 §4.5 NFR-02 / AC-40 / AC-85）。
/// 仅在 <c>OBJECT1688_PERF_BENCH=1</c> 时执行（CI perf-bench 任务：`--filter FullyQualifiedName~RuleMatcherBench`）；
/// 常规测试运行直接返回（不拖慢单测）。
/// 单轮预算目标 &lt; 5ms；此处打印实测值并设宽松上限（<25ms）防止 CI 抖动误报（历史退化对比由 CI Artifact 承担）。
/// </summary>
public class RuleMatcherBenchTests
{
    private const int RuleCount = 500;
    private const int ProcessTargets = 150;
    private const int WindowTargets = 150;
    private const int Iterations = 200;

    [Fact]
    public void Bench_500Rules_ReportsPerPollCost()
    {
        if (Environment.GetEnvironmentVariable("OBJECT1688_PERF_BENCH") != "1")
        {
            return; // 仅基准任务运行
        }

        var rules = BuildRules(RuleCount);
        var global = new GlobalConfig
        {
            MonitorPollIntervalMs = 1500,
            MinTriggerIntervalSeconds = 2,
            DedupeWindowSeconds = 10,
        };
        var engine = new MonitorEngine(rules, global, new HashSet<int>());
        var targets = BuildTargets();
        var livePids = targets.Select(static t => t.ProcessId).ToHashSet();
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 20; i++)
        {
            _ = engine.Evaluate(targets, livePids, now); // 预热（JIT/索引）
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
        {
            _ = engine.Evaluate(targets, livePids, now);
        }

        sw.Stop();
        var avgMs = sw.Elapsed.TotalMilliseconds / Iterations;
        Console.WriteLine($"[PERF] {RuleCount} rules x {targets.Count} targets: avg {avgMs:F3} ms/poll (target <5ms)");

        Assert.True(avgMs < 25, $"500-rule match too slow: {avgMs:F3} ms/poll (target <5ms)");
    }

    private static List<RuleConfig> BuildRules(int count)
    {
        var rules = new List<RuleConfig>(count);
        for (var i = 0; i < count; i++)
        {
            var mode = (i % 3) switch
            {
                0 => MatchMode.Exact,
                1 => MatchMode.Contains,
                _ => MatchMode.Wildcard,
            };
            var type = i % 4 == 0 ? MatchType.WindowTitle : MatchType.Process;
            var value = type == MatchType.Process
                ? mode == MatchMode.Wildcard ? $"proc{i}*" : $"proc{i}.exe"
                : mode == MatchMode.Wildcard ? $"*title{i}*" : $"title{i}";

            rules.Add(new RuleConfig
            {
                RuleId = $"r-{i:D4}",
                MatchType = type,
                MatchValue = value,
                MatchMode = mode,
                Enabled = true,
                DisplayLines = new[] { new DisplayLine { Text = $"line {i}" } },
            });
        }

        return rules;
    }

    private static List<MatchTarget> BuildTargets()
    {
        var targets = new List<MatchTarget>(ProcessTargets + WindowTargets);
        for (var i = 0; i < ProcessTargets; i++)
        {
            targets.Add(MatchTarget.ForProcess($"proc{i}.exe", isFullscreen: i % 10 == 0, processId: 1000 + i));
        }

        for (var i = 0; i < WindowTargets; i++)
        {
            targets.Add(MatchTarget.ForWindow($"title{i} window", isFullscreen: false, processId: 5000 + i));
        }

        return targets;
    }
}
