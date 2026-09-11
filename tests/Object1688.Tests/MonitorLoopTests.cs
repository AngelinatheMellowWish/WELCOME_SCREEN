using Object1688.Shared.Config;
using Object1688.Shared.Monitor;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;
using MatchMode = Object1688.Shared.Config.MatchMode;
using RuleConfig = Object1688.Shared.Config.RuleConfig;

namespace Object1688.Tests;

/// <summary>
/// MonitorLoop 单测（架构 §4.1/§4.5，AC-45，M3c）。
/// 契约点：进程/窗口快照 → 目标构建 → 引擎评估的整链路回调分发；
/// AC-45 自身进程排除（含子目录不误判、每轮动态刷新 PID 集合）；
/// AC-05 全屏传播（进程目标携带「该进程有全屏窗口」标志 → includeFullscreen=false 规则不匹配）；
/// 配置引用变化热重建引擎（ConfigChanged 新实例生效）；
/// 快照异常 → onError 回调且不抛出、照常返回轮询间隔；
/// minInterval 抑制回调（MON-W-5005 语义）与进程存活期去重抑制回调。
/// </summary>
public class MonitorLoopTests
{
    // ---- 构造辅助 ----

    private const string SelfDir = @"C:\Program Files\Object1688";

    private static AppConfig Config(int pollIntervalMs = 1500, int minInterval = 2, params RuleConfig[] rules) => new()
    {
        Global = new GlobalConfig
        {
            MonitorPollIntervalMs = pollIntervalMs,
            MinTriggerIntervalSeconds = minInterval,
            DedupeWindowSeconds = 10,
        },
        Welcome = new WelcomeConfig
        {
            DisplayLines = [new DisplayLine { Text = "欢迎" }],
        },
        Manual = new ManualConfig
        {
            DisplayLines = [new DisplayLine { Text = "手动" }],
        },
        Rules = rules,
        Dnd = new DndConfig { Schedule = [] },
        Sound = new SoundConfig(),
    };

    private static RuleConfig Rule(
        string id,
        string value,
        MatchType type = MatchType.Process,
        MatchMode mode = MatchMode.Exact,
        bool includeFullscreen = true,
        double? minInterval = null) => new()
    {
        RuleId = id,
        MatchType = type,
        MatchValue = value,
        MatchMode = mode,
        IncludeFullscreen = includeFullscreen,
        MinIntervalSeconds = minInterval,
        DisplayLines = [new DisplayLine { Text = $"{id} 大字" }],
    };

    private sealed class FakeSnapshotProvider : IMonitorSnapshotProvider
    {
        public MonitorSnapshot Snapshot { get; set; } = new([], []);
        public Exception? ThrowOnTake { get; set; }

        public MonitorSnapshot TakeSnapshot()
        {
            if (ThrowOnTake is not null)
            {
                throw ThrowOnTake;
            }

            return Snapshot;
        }
    }

    private static (MonitorLoop Loop, List<MonitorTrigger> Triggers, List<MonitorSuppression> Suppressions, List<string> Errors) CreateLoop(
        FakeSnapshotProvider provider,
        Func<AppConfig> configProvider,
        string selfExecutableDirectory = SelfDir)
    {
        var triggers = new List<MonitorTrigger>();
        var suppressions = new List<MonitorSuppression>();
        var errors = new List<string>();
        var loop = new MonitorLoop(configProvider, provider, selfExecutableDirectory, triggers.Add, suppressions.Add, errors.Add);
        return (loop, triggers, suppressions, errors);
    }

    private static MonitorProcessInfo Process(int pid, string name, string? exePath = null) => new(pid, name, exePath);

    // ---- 基础触发 ----

    [Fact]
    public void PollOnce_ProcessExactHit_InvokesTriggerCallback()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var (loop, triggers, suppressions, _) = CreateLoop(provider, () => Config(rules: [Rule("r1", "chrome.exe")]));

        var interval = loop.PollOnce();

        Assert.Equal(1500, interval);
        var trigger = Assert.Single(triggers);
        Assert.Equal("r1", trigger.Rule.RuleId);
        Assert.Equal(MatchType.Process, trigger.Target.Type);
        Assert.Equal("chrome.exe", trigger.Target.Text);
        Assert.Equal(100, trigger.Target.ProcessId);
        Assert.False(trigger.Target.IsFullscreen);
        Assert.False(trigger.IsReappearTrigger);
        Assert.Empty(suppressions);
    }

    [Fact]
    public void PollOnce_DuringPause_SkipsEvaluation()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var (loop, triggers, _, _) = CreateLoop(provider, () => Config(rules: [Rule("r1", "chrome.exe")]));

        loop.PauseFor(TimeSpan.FromMinutes(5));
        var interval = loop.PollOnce();

        Assert.Equal(1500, interval);
        Assert.Empty(triggers); // 暂停窗口内不评估（AC-41 唤醒抖动抑制）
    }

    [Fact]
    public void PollOnce_WindowTitleContainsHit_InvokesTriggerCallback()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([], [new MonitorWindowInfo(200, "记事本 - 未命名", IsFullscreen: false)]),
        };
        var (loop, triggers, suppressions, _) = CreateLoop(
            provider,
            () => Config(rules: [Rule("r1", "记事本", MatchType.WindowTitle, MatchMode.Contains)]));

        loop.PollOnce();

        var trigger = Assert.Single(triggers);
        Assert.Equal("r1", trigger.Rule.RuleId);
        Assert.Equal(MatchType.WindowTitle, trigger.Target.Type);
        Assert.Equal("记事本 - 未命名", trigger.Target.Text);
        Assert.Equal(200, trigger.Target.ProcessId);
        Assert.Empty(suppressions);
    }

    // ---- AC-45 自身进程排除 ----

    [Fact]
    public void PollOnce_SelfProcessSameDirectory_Excluded_SubdirectoryNot()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot(
            [
                Process(300, "Object1688.Main.exe", @"C:\Program Files\Object1688\Object1688.Main.exe"),  // 自目录 → 排除
                Process(301, "Object1688.Main.exe", @"C:\Program Files\Object1688\sub\Object1688.Main.exe"), // 子目录 → 不算自身
            ], []),
        };
        var (loop, triggers, suppressions, _) = CreateLoop(provider, () => Config(rules: [Rule("r1", "Object1688.Main.exe")]));

        loop.PollOnce();

        // 仅子目录进程命中（AC-45：同目录判定不含子目录）
        var trigger = Assert.Single(triggers);
        Assert.Equal(301, trigger.Target.ProcessId);
        Assert.Empty(suppressions);
    }

    [Fact]
    public void PollOnce_SelfPids_RefreshEachRound_DynamicExclusion()
    {
        // 每轮动态刷新：第一轮进程在自目录被排除；第二轮同 PID 移到外部目录 → 恢复为候选目标
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Program Files\Object1688\chrome.exe")], []),
        };
        var (loop, triggers, _, _) = CreateLoop(provider, () => Config(rules: [Rule("r1", "chrome.exe")]));

        loop.PollOnce();
        Assert.Empty(triggers); // 第一轮：自目录 → AC-45 排除

        provider.Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\temp\chrome.exe")], []);
        loop.PollOnce();

        var trigger = Assert.Single(triggers); // 第二轮：移除自目录 → 触发
        Assert.Equal(100, trigger.Target.ProcessId);
    }

    // ---- AC-05 全屏传播 ----

    [Fact]
    public void PollOnce_FullscreenWindow_PropagatesToProcessTarget_IncludeFullscreenFalse_NoHit()
    {
        // 进程有全屏窗口 → 进程目标 IsFullscreen=true → includeFullscreen=false 规则不匹配（AC-05）
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot(
                [Process(400, "game.exe", @"C:\Game\game.exe")],
                [new MonitorWindowInfo(400, "Game", IsFullscreen: true)]),
        };
        var (loop, triggers, suppressions, _) = CreateLoop(
            provider,
            () => Config(rules: [Rule("r1", "game.exe", includeFullscreen: false)]));

        loop.PollOnce();

        Assert.Empty(triggers);
        Assert.Empty(suppressions); // 全屏排除是匹配期静默跳过（非抑制记录）
    }

    [Fact]
    public void PollOnce_FullscreenProcessSameRule_IncludeFullscreenTrue_Triggers()
    {
        // 对照：includeFullscreen=true（缺省）时全屏目标照常触发（AC-05 可配置化）
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot(
                [Process(400, "game.exe", @"C:\Game\game.exe")],
                [new MonitorWindowInfo(400, "Game", IsFullscreen: true)]),
        };
        var (loop, triggers, _, _) = CreateLoop(
            provider,
            () => Config(rules: [Rule("r1", "game.exe", includeFullscreen: true)]));

        loop.PollOnce();

        var trigger = Assert.Single(triggers);
        Assert.True(trigger.Target.IsFullscreen);
    }

    // ---- 抑制回调 ----

    [Fact]
    public void PollOnce_MinInterval_InvokesSuppressionCallback()
    {
        // 规则级 minInterval=60：不同 PID 连续命中同规则 → 第二次抑制（MON-W-5005 语义）
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var config = Config(rules: [Rule("r1", "chrome.exe", minInterval: 60)]);
        var (loop, triggers, suppressions, _) = CreateLoop(provider, () => config);

        loop.PollOnce();
        Assert.Single(triggers);

        provider.Snapshot = new MonitorSnapshot([Process(101, "chrome.exe", @"C:\Chrome\chrome.exe")], []);
        loop.PollOnce();

        Assert.Single(triggers); // 无新触发
        var suppression = Assert.Single(suppressions);
        Assert.Equal(SuppressionReason.MinInterval, suppression.Reason);
        Assert.Equal("r1", suppression.Rule.RuleId);
        Assert.Equal(101, suppression.Target.ProcessId);
    }

    [Fact]
    public void PollOnce_SamePidAliveAcrossRounds_SecondPoll_DedupeSuppression()
    {
        // 进程存活期去重：同 PID 第二轮 → DedupeWindow 抑制（正常语义，非错误码）
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var config = Config(rules: [Rule("r1", "chrome.exe")]);
        var (loop, triggers, suppressions, _) = CreateLoop(provider, () => config);

        loop.PollOnce();
        provider.Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []);
        loop.PollOnce();

        Assert.Single(triggers);
        var suppression = Assert.Single(suppressions);
        Assert.Equal(SuppressionReason.DedupeWindow, suppression.Reason);
    }

    [Fact]
    public void PollOnce_ProcessVanishedThenReappears_ReappearTriggerCallback()
    {
        // 消失 → 去重记录清除+登记待补触；重现 → 补触（IsReappearTrigger=true，minInterval 豁免）
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var config = Config(rules: [Rule("r1", "chrome.exe", minInterval: 60)]);
        var (loop, triggers, _, _) = CreateLoop(provider, () => config);

        loop.PollOnce();
        Assert.Single(triggers);

        provider.Snapshot = new MonitorSnapshot([], []); // 进程消失
        loop.PollOnce();
        Assert.Single(triggers); // 无新触发

        provider.Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []);
        loop.PollOnce();

        Assert.Equal(2, triggers.Count); // 重现补触
        Assert.True(triggers[^1].IsReappearTrigger);
    }

    // ---- 配置热更新 ----

    [Fact]
    public void PollOnce_ConfigReferenceChanged_RebuildsEngine_NewRuleTakesEffect()
    {
        // 引用变化（ConfigChanged 广播新实例）→ 引擎重建：新规则生效、旧规则不再匹配
        var current = Config(rules: [Rule("r1", "chrome.exe")]);
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var (loop, triggers, _, _) = CreateLoop(provider, () => current);

        loop.PollOnce();
        Assert.Single(triggers);
        Assert.Equal("r1", triggers[0].Rule.RuleId);

        current = Config(rules: [Rule("r2", "firefox.exe")]); // 引用替换 → 下轮重建引擎
        provider.Snapshot = new MonitorSnapshot([Process(101, "firefox.exe", @"C:\Firefox\firefox.exe")], []);
        loop.PollOnce();

        Assert.Equal(2, triggers.Count);
        Assert.Equal("r2", triggers[^1].Rule.RuleId); // 新规则命中

        // 旧规则在重建后不再活动：chrome 进程不再触发 r1
        provider.Snapshot = new MonitorSnapshot([Process(102, "chrome.exe", @"C:\Chrome\chrome.exe")], []);
        loop.PollOnce();
        Assert.Equal(2, triggers.Count);
    }

    // ---- 快照异常与返回间隔 ----

    [Fact]
    public void PollOnce_SnapshotThrows_InvokesOnError_NoThrow_ReturnsInterval()
    {
        var provider = new FakeSnapshotProvider { ThrowOnTake = new InvalidOperationException("boom") };
        var (loop, triggers, _, errors) = CreateLoop(provider, () => Config(rules: [Rule("r1", "chrome.exe")]));

        var interval = loop.PollOnce(); // 不得抛出

        Assert.Equal(1500, interval);
        Assert.Empty(triggers);
        var error = Assert.Single(errors);
        Assert.Contains("boom", error);
    }

    [Fact]
    public void PollOnce_ReturnsConfiguredPollInterval()
    {
        var provider = new FakeSnapshotProvider();
        var (loop, triggers, _, _) = CreateLoop(provider, () => Config(pollIntervalMs: 777));

        Assert.Equal(777, loop.PollOnce());
        Assert.Empty(triggers);
    }

    // ---- 空白目标过滤 ----

    [Fact]
    public void PollOnce_BlankProcessNameAndWindowTitle_Skipped_NoTargets()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot(
                [Process(100, "   ", @"C:\x\y.exe")],
                [new MonitorWindowInfo(200, "", IsFullscreen: false)]),
        };
        var (loop, triggers, suppressions, _) = CreateLoop(
            provider,
            () => Config(rules:
            [
                Rule("r1", "   ", MatchType.Process, MatchMode.Contains),
                Rule("r2", "", MatchType.WindowTitle, MatchMode.Contains),
            ]));

        loop.PollOnce();

        Assert.Empty(triggers);
        Assert.Empty(suppressions);
    }

    // ---- RunAsync 取消 ----

    [Fact]
    public async Task RunAsync_CancellationRequested_Returns()
    {
        var provider = new FakeSnapshotProvider
        {
            Snapshot = new MonitorSnapshot([Process(100, "chrome.exe", @"C:\Chrome\chrome.exe")], []),
        };
        var (loop, _, _, _) = CreateLoop(provider, () => Config(rules: [Rule("r1", "chrome.exe")]));
        using var cts = new CancellationTokenSource();

        var runTask = loop.RunAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        await runTask; // 取消后 Task.Delay 抛 OCE → 正常返回，不抛出
    }
}