using Object1688.Shared.Config;

namespace Object1688.Shared.Monitor;

/// <summary>
/// 单轮监测评估结果（架构 §4.4）。
/// <see cref="Triggers"/> 为应进入播放队列的触发候选（已通过去重/minInterval 裁决）；
/// <see cref="Suppressions"/> 为被抑制的命中（进程存活去重 / minInterval，后者对应 MON-W-5005）。
/// </summary>
public sealed record MonitorEvaluation(
    IReadOnlyList<MonitorTrigger> Triggers,
    IReadOnlyList<MonitorSuppression> Suppressions);

/// <summary>一次应触发的命中（架构 §4.4）；<see cref="IsReappearTrigger"/> = 进程消失重现补触（minInterval 豁免）。</summary>
public sealed record MonitorTrigger(RuleConfig Rule, MatchTarget Target, bool IsReappearTrigger);

/// <summary>一次被抑制的命中（§4.4：进程存活去重 / minInterval 双维度）。</summary>
public sealed record MonitorSuppression(RuleConfig Rule, MatchTarget Target, SuppressionReason Reason);

/// <summary>抑制原因（MON-W-5005 对应 MinInterval；进程存活去重为正常语义不记错误码）。</summary>
public enum SuppressionReason
{
    /// <summary>同进程 PID 存活期去重（§4.4/AC-46：进程仍在期间不重复触发，含去重窗口过后）。</summary>
    DedupeWindow,

    /// <summary>同规则最小触发间隔（规则级 minIntervalSeconds ?? 全局 minTriggerIntervalSeconds，§4.4 双维度叠加）。</summary>
    MinInterval,
}

/// <summary>
/// 监测引擎（架构 §4.1/§4.4，AC-45/AC-46，M3b）。
/// 状态机职责：对一批快照目标做规则匹配 → 逐命中裁决。
/// 去重语义（§4.4/AC-46）：以进程 PID 为去重键——同一 PID 命中规则后，进程存活期间不重复触发
/// （无论开多少个窗口、去重窗口是否已过）；进程消失（不在 live 集合）后去重记录清除，
/// 重现时重新计数触发（补触豁免 minInterval）。
/// minInterval（§4.4）：同一规则两次实际触发间隔 &lt; 规则级 minIntervalSeconds（?? 全局兜底）时抑制并记 MON-W-5005——
/// 与进程存活去重双维度叠加，均满足才放行，防止"去重窗口调小 + 多窗口/频繁命中"造成大字连发轰炸。
/// 自身进程排除（AC-45）：构造时传入本程序 PID 集合，匹配前过滤，杜绝自触发。
/// 线程模型：单实例由 Overlay 监测循环单线程驱动（M3c），无内部锁。
/// </summary>
public sealed class MonitorEngine
{
    private readonly RuleMatcher _matcher;
    private readonly ISet<int> _selfProcessIds;
    private readonly double _defaultMinIntervalSeconds;
    private readonly Dictionary<string, Dictionary<int, DateTimeOffset>> _dedupeByRule = new(); // ruleId → pid → 最近触发（随进程存活期存在）
    private readonly Dictionary<string, DateTimeOffset> _lastTriggerByRule = new();             // ruleId → 最近实际触发（minInterval 维度）
    private readonly Dictionary<string, HashSet<int>> _vanishedByRule = new();                  // ruleId → 已消失的已触发 PID（重现补触）

    /// <summary>
    /// 构建监测引擎。
    /// </summary>
    /// <param name="rules">全部触发规则（disabled 由 <see cref="RuleMatcher"/> 在索引构建期排除，§4.1）。</param>
    /// <param name="global">全局配置（minTriggerIntervalSeconds 兜底，§5.7；dedupeWindowSeconds 语义在 §4.4）。</param>
    /// <param name="selfProcessIds">本程序进程 PID 集合（中控/叠加/监测/配置/日志五进程，AC-45 自身排除）。</param>
    public MonitorEngine(IReadOnlyList<RuleConfig> rules, GlobalConfig global, ISet<int> selfProcessIds)
    {
        _matcher = new RuleMatcher(rules);
        _selfProcessIds = selfProcessIds;
        _defaultMinIntervalSeconds = global.MinTriggerIntervalSeconds;
    }

    /// <summary>
    /// 执行一轮监测评估（单次进程快照 + 窗口枚举 → 触发候选）。
    /// 每轮先清理已消失进程的去重记录（record 随存活期存在；消失 → 记录清除待重现），再逐目标匹配与裁决。
    /// </summary>
    /// <param name="targets">本轮快照目标（调用方已枚举进程/窗口，可含自身进程——引擎仍做 AC-45 过滤）。</param>
    /// <param name="liveProcessIds">本轮仍在运行的进程 PID 集合（用于消失判定；含自身进程亦无妨）。</param>
    /// <param name="now">评估时点（UTC）。</param>
    /// <returns>触发候选 + 抑制命中。</returns>
    public MonitorEvaluation Evaluate(IReadOnlyList<MatchTarget> targets, IReadOnlySet<int> liveProcessIds, DateTimeOffset now)
    {
        CleanVanished(liveProcessIds);

        var triggers = new List<MonitorTrigger>();
        var suppressions = new List<MonitorSuppression>();

        foreach (var target in targets)
        {
            if (_selfProcessIds.Contains(target.ProcessId))
            {
                continue; // AC-45 自身进程排除
            }

            foreach (var rule in _matcher.Match(target))
            {
                Decide(rule, target, now, triggers, suppressions);
            }
        }

        return new MonitorEvaluation(triggers, suppressions);
    }

    /// <summary>清除已消失进程的去重记录：不在 live 集合 → 移除记录并登记待补触（重现时重新计数，§4.4/AC-46）。</summary>
    private void CleanVanished(IReadOnlySet<int> liveProcessIds)
    {
        foreach (var (ruleId, pidMap) in _dedupeByRule)
        {
            var vanishedPids = pidMap.Keys.Where(pid => !liveProcessIds.Contains(pid)).ToList();
            foreach (var pid in vanishedPids)
            {
                pidMap.Remove(pid);
                if (!_vanishedByRule.TryGetValue(ruleId, out var set))
                {
                    set = [];
                    _vanishedByRule[ruleId] = set;
                }

                set.Add(pid);
            }
        }
    }

    private void Decide(
        RuleConfig rule,
        MatchTarget target,
        DateTimeOffset now,
        List<MonitorTrigger> triggers,
        List<MonitorSuppression> suppressions)
    {
        // 消失重现补触（§4.4/AC-46）：进程消失后再现 → 重新计数触发，minInterval 豁免
        if (_vanishedByRule.TryGetValue(rule.RuleId, out var vanished) && vanished.Remove(target.ProcessId))
        {
            triggers.Add(new MonitorTrigger(rule, target, IsReappearTrigger: true));
            RecordTrigger(rule, target.ProcessId, now);
            return;
        }

        // 进程存活期去重（§4.4/AC-46）：记录随进程存活存在——同 PID 无论如何重复命中（多窗口/窗口过后）一次即止
        if (_dedupeByRule.TryGetValue(rule.RuleId, out var pidMap) && pidMap.ContainsKey(target.ProcessId))
        {
            suppressions.Add(new MonitorSuppression(rule, target, SuppressionReason.DedupeWindow));
            return;
        }

        // 同规则最小间隔（§4.4；规则级 ?? 全局兜底，双维度叠加）：同一规则两次实际触发间隔 < minInterval → 抑制记 MON-W-5005
        var minInterval = rule.MinIntervalSeconds ?? _defaultMinIntervalSeconds;
        if (minInterval > 0 &&
            _lastTriggerByRule.TryGetValue(rule.RuleId, out var lastActual) &&
            (now - lastActual).TotalSeconds < minInterval)
        {
            suppressions.Add(new MonitorSuppression(rule, target, SuppressionReason.MinInterval));
            return;
        }

        triggers.Add(new MonitorTrigger(rule, target, IsReappearTrigger: false));
        RecordTrigger(rule, target.ProcessId, now);
    }

    private void RecordTrigger(RuleConfig rule, int processId, DateTimeOffset now)
    {
        if (!_dedupeByRule.TryGetValue(rule.RuleId, out var pidMap))
        {
            pidMap = [];
            _dedupeByRule[rule.RuleId] = pidMap;
        }

        pidMap[processId] = now;
        _lastTriggerByRule[rule.RuleId] = now; // minInterval 维度：仅记「实际触发」
    }
}