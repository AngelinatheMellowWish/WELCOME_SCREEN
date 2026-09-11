using System.Text.RegularExpressions;
// System.IO.MatchType 与配置 MatchType 同名（SDK 隐式 using），按测试工程既有别名模式消歧
using MatchType = Object1688.Shared.Config.MatchType;
using MatchMode = Object1688.Shared.Config.MatchMode;
using RuleConfig = Object1688.Shared.Config.RuleConfig;

namespace Object1688.Shared.Monitor;

/// <summary>
/// 规则匹配器（架构 §4.2/§4.5，F-10 三级匹配）。
/// 构建期按匹配类型/模式分桶预索引（进程精确 / 进程非精确 / 窗口标题精确 / 窗口标题非精确），
/// 通配模式（* 多字符 / ? 单字符）预编译为受控正则（§4.2 注），避免每目标重编译；
/// 匹配期精确桶 O(1) 取回、非精确桶线性扫描（500 条规则单轮预算 &lt; 5ms，AC-40/NFR-02）。
/// 命中规则按配置顺序输出（同命中优先级 = rules[] 顺序，§4.2 注）。
/// disabled 规则在索引构建期即排除（§4.1 匹配优先级：先判 enabled），不占扫描预算。
/// 性能要点（AC-40）：非精确规则以「表达式元数据列表」承载（模式/大小写/预编译正则，免每调用字典查找）；
/// 大小写不敏感时目标仅小写一次、模式预小写后以 Ordinal 比较；通配用 NonBacktracking 线性引擎。
/// </summary>
public sealed class RuleMatcher
{
    private sealed record ExpressionRule(int Index, RuleConfig Rule, string Pattern, bool CaseSensitive, Regex? Wildcard);

    private readonly List<RuleConfig> _rules;
    private readonly Dictionary<string, List<int>> _exactProcessIgnoreCase = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<int>> _exactProcessCaseSensitive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<int>> _exactTitleIgnoreCase = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<int>> _exactTitleCaseSensitive = new(StringComparer.Ordinal);
    private readonly List<ExpressionRule> _processExpression = []; // contains/wildcard → 线性
    private readonly List<ExpressionRule> _titleExpression = [];    // contains/wildcard → 线性
    private readonly bool _anyExactCaseSensitive;
    private readonly bool _anyIgnoreCaseExpression;

    /// <summary>
    /// 以规则集构建索引（disabled 规则直接排除，F-20/§4.1 匹配优先级）。
    /// </summary>
    /// <param name="rules">全部触发规则。</param>
    public RuleMatcher(IReadOnlyList<RuleConfig> rules)
    {
        _rules = rules.ToList();

        for (var i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            if (!rule.Enabled)
            {
                continue; // §4.1 匹配优先级：先判 enabled，disabled 不占索引
            }

            var isProcess = rule.MatchType == MatchType.Process;

            switch (rule.MatchMode)
            {
                case MatchMode.Exact:
                    var bucket = (isProcess, rule.MatchCaseSensitive) switch
                    {
                        (true, false) => _exactProcessIgnoreCase,
                        (true, true) => _exactProcessCaseSensitive,
                        (false, false) => _exactTitleIgnoreCase,
                        _ => _exactTitleCaseSensitive,
                    };
                    AddToBucket(bucket, rule.MatchValue, i);
                    break;

                case MatchMode.Contains:
                case MatchMode.Wildcard:
                {
                    // 不敏感规则：模式预小写；匹配期目标小写一次后用 Ordinal 比较（AC-40 提速）
                    var caseSensitive = rule.MatchCaseSensitive;
                    string pattern;
                    Regex? wildcard = null;
                    if (rule.MatchMode == MatchMode.Wildcard && TryExtractSimpleContains(rule.MatchValue, out var inner))
                    {
                        // `*X*` 型通配退化为 Contains（Ordinal），避开正则调用开销（AC-40）
                        pattern = caseSensitive ? inner : inner.ToLowerInvariant();
                    }
                    else
                    {
                        pattern = caseSensitive ? rule.MatchValue : rule.MatchValue.ToLowerInvariant();
                        if (rule.MatchMode == MatchMode.Wildcard)
                        {
                            wildcard = CompileWildcard(pattern);
                        }
                    }

                    (isProcess ? _processExpression : _titleExpression)
                        .Add(new ExpressionRule(i, rule, pattern, caseSensitive, wildcard));
                    if (!caseSensitive)
                    {
                        _anyIgnoreCaseExpression = true;
                    }

                    break;
                }
            }
        }

        _anyExactCaseSensitive = _rules.Any(static r => r.MatchCaseSensitive);
    }

    /// <summary>
    /// 对单个目标执行匹配。
    /// </summary>
    /// <param name="target">进程名或窗口标题目标（§4.1）。</param>
    /// <returns>命中规则（按配置顺序），空 = 未命中（MON-W-5002 由调用方记录）。</returns>
    public IReadOnlyList<RuleConfig> Match(MatchTarget target)
    {
        // 收集命中规则索引（每路径天然去重，避免同规则多桶/多路径重复）
        var hitIndexes = new HashSet<int>();
        var lowerText = _anyIgnoreCaseExpression ? target.Text.ToLowerInvariant() : target.Text;

        if (target.Type == MatchType.Process)
        {
            CollectBucket(_exactProcessIgnoreCase, target.Text, hitIndexes);
            if (_anyExactCaseSensitive)
            {
                CollectBucket(_exactProcessCaseSensitive, target.Text, hitIndexes);
            }

            foreach (var expr in _processExpression)
            {
                TryExpression(expr, target.Text, lowerText, hitIndexes);
            }
        }
        else
        {
            CollectBucket(_exactTitleIgnoreCase, target.Text, hitIndexes);
            if (_anyExactCaseSensitive)
            {
                CollectBucket(_exactTitleCaseSensitive, target.Text, hitIndexes);
            }

            foreach (var expr in _titleExpression)
            {
                TryExpression(expr, target.Text, lowerText, hitIndexes);
            }
        }

        if (hitIndexes.Count == 0)
        {
            return [];
        }

        // 按配置顺序输出 + 统一全屏裁决（AC-05）
        var hits = new List<RuleConfig>(hitIndexes.Count);
        for (var i = 0; i < _rules.Count; i++)
        {
            if (!hitIndexes.Contains(i))
            {
                continue;
            }

            var rule = _rules[i];
            if (!rule.IncludeFullscreen && target.IsFullscreen)
            {
                continue; // 全屏应用不触发该规则（AC-05）
            }

            hits.Add(rule);
        }

        return hits;
    }

    private static void TryExpression(ExpressionRule expr, string text, string lowerText, HashSet<int> hitIndexes)
    {
        var source = expr.CaseSensitive ? text : lowerText;
        bool matched;
        if (expr.Wildcard is not null)
        {
            // 通配正则超时（病态模式或重负载下墙钟超时）：按不匹配处理，绝不外抛拖垮监测循环
            try
            {
                matched = expr.Wildcard.IsMatch(source);
            }
            catch (RegexMatchTimeoutException)
            {
                matched = false;
            }
        }
        else
        {
            matched = source.Contains(expr.Pattern, StringComparison.Ordinal);
        }

        if (matched)
        {
            hitIndexes.Add(expr.Index);
        }
    }

    private static void AddToBucket(Dictionary<string, List<int>> bucket, string key, int index)
    {
        if (!bucket.TryGetValue(key, out var list))
        {
            list = [];
            bucket[key] = list;
        }

        list.Add(index);
    }

    private static void CollectBucket(Dictionary<string, List<int>> bucket, string text, HashSet<int> hitIndexes)
    {
        if (bucket.TryGetValue(text, out var indexes))
        {
            foreach (var idx in indexes)
            {
                hitIndexes.Add(idx);
            }
        }
    }

    /// <summary>识别 `*X*` 型通配（首尾为 *，中间无其他 * / ?）→ 可退化为 Contains（Ordinal），避开正则开销。</summary>
    private static bool TryExtractSimpleContains(string pattern, out string inner)
    {
        inner = string.Empty;
        if (pattern.Length >= 2 && pattern[0] == '*' && pattern[^1] == '*')
        {
            var mid = pattern[1..^1];
            if (mid.Length > 0 && mid.IndexOfAny(new[] { '*', '?' }) < 0)
            {
                inner = mid;
                return true;
            }
        }

        return false;
    }

    /// <summary>通配转译（§4.2 注）：锚定整串，* → .*、? → .，其余字符转义为字面量；
    /// 采用 <see cref="RegexOptions.NonBacktracking"/>（线性时间、无灾难回溯）提升 500 规则预算（AC-40）。
    /// 调用方已按大小写策略传入（不敏感场景模式与目标均已预小写），此处不再启用 IgnoreCase。</summary>
    private static Regex CompileWildcard(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        var body = escaped
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal);
        return new Regex("^" + body + "$", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    }
}
