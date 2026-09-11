using System.Text.RegularExpressions;
using Object1688.Shared.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 配置校验器（架构 §5.3 全量校验 + §5.6 F-29 冲突/重复检测）。
/// 保存/导入前对完整配置执行 §5.3 规则：匹配值非空、matchMode 合法（枚举约束由模型承载）、
/// 时长 ∈ [0, MaxDurationSeconds]、描边色可解析（#RGB/#RGBA/#RRGGBB/#AARRGGBB）、通配语法合法。
/// 另提供 <see cref="CheckConflicts"/>：完全重复 → error（CFG-V-1003），匹配范围包含/重叠 → warning。
/// 本类只读校验，不修改输入；返回的 ConfigIssue 与加载链路共用同一错误码体系。
/// </summary>
public static class ConfigValidator
{
    /// <summary>描边色/文字色十六进制格式（含 3/4/6/8 位短格式，与加载器一致）。</summary>
    private static readonly Regex HexColorRegex = new(
        @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>通配语法合法性：仅允许普通文本 + * / ?（与 <see cref="ConfigLoader"/> 通配校验一致）。</summary>
    private static readonly Regex WildcardPatternRegex = new(
        @"^[^\[\](){}^$+|]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 对完整配置执行 §5.3 全量校验。
    /// </summary>
    /// <param name="config">待校验配置。</param>
    /// <returns>问题清单（空 = 合法）；error 项为 CFG-V-1003（保存前须拦截），warning 项为提示不阻断。</returns>
    public static IReadOnlyList<ConfigIssue> Validate(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var issues = new List<ConfigIssue>();

        // 欢迎大字 / 手动大字：多行段落合法性（F-06：至少一行、每行文字非空、字号正数）
        ValidateDisplayLines(config.Welcome.DisplayLines, "welcome.displayLines", issues);
        ValidateDisplayLines(config.Manual.DisplayLines, "manual.displayLines", issues);

        // 描边方式（架构 §3.3）：全局必须为 shadow/stroke；手动空串 = 跟随全局
        if (!IsValidOutlineMode(config.Global.OutlineMode))
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"global.outlineMode 非法（需 shadow / stroke）：{config.Global.OutlineMode}"));
        }

        if (!string.IsNullOrWhiteSpace(config.Manual.OutlineMode) && !IsValidOutlineMode(config.Manual.OutlineMode))
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"manual.outlineMode 非法（需 shadow / stroke）：{config.Manual.OutlineMode}"));
        }

        // 时长范围：欢迎/手动大字节
        ValidateDuration(config.Welcome.DelaySeconds, "welcome.delaySeconds", issues);
        ValidateDuration(config.Welcome.HoldSeconds, "welcome.holdSeconds", issues);
        ValidateDuration(config.Manual.DelaySeconds, "manual.delaySeconds", issues);
        ValidateDuration(config.Manual.HoldSeconds, "manual.holdSeconds", issues);

        // 勿扰时段：时间格式 HH:mm（架构 §5.7 / CFG-V-1009 语义）
        foreach (var (entry, index) in config.Dnd.Schedule.Select((e, i) => (e, i)))
        {
            if (!TimeOnly.TryParseExact(entry.Start, "HH:mm", out _))
            {
                issues.Add(Issue(ErrorCodes.ConfigTimeFormatInvalid, $"dnd.schedule[{index}].start 非法（需 HH:mm）：{entry.Start}"));
            }

            if (!TimeOnly.TryParseExact(entry.End, "HH:mm", out _))
            {
                issues.Add(Issue(ErrorCodes.ConfigTimeFormatInvalid, $"dnd.schedule[{index}].end 非法（需 HH:mm）：{entry.End}"));
            }
        }

        // 自定义提示音资源校验（AC-72 / CFG-V-1007）：Source=custom 时校验格式/大小/路径安全
        if (string.Equals(config.Sound.Source, "custom", StringComparison.OrdinalIgnoreCase))
        {
            issues.AddRange(SoundAssetValidator.Validate(config.Sound.CustomPath, AppContext.BaseDirectory));
        }

        foreach (var rule in config.Rules)
        {
            ValidateRule(rule, issues);
        }

        return issues;
    }

    /// <summary>
    /// 规则冲突/重复检测（F-29/AC-72）：完全重复（error）与匹配范围包含/重叠（warning）。
    /// </summary>
    /// <param name="rules">规则列表（含禁用规则：disabled 规则保留但 note 提示不参与冲突判定）。</param>
    /// <returns>问题清单：重复 → CFG-V-1003（error，保存前须拦截）；重叠 → CFG-V-1003 消息内注明 warning（不阻断保存）。</returns>
    public static IReadOnlyList<ConfigIssue> CheckConflicts(IReadOnlyList<RuleConfig> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var issues = new List<ConfigIssue>();
        var list = rules.ToList();

        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                var a = list[i];
                var b = list[j];

                if (a.MatchType != b.MatchType)
                {
                    continue; // 进程名与窗口标题互不冲突
                }

                var valueEqual = string.Equals(a.MatchValue, b.MatchValue, Comparison(a) & Comparison(b));
                if (a.MatchMode == b.MatchMode && valueEqual)
                {
                    // 完全重复（同类型/同模式/同值）
                    issues.Add(Issue(
                        ErrorCodes.ConfigValidationFailed,
                        $"规则 [{a.RuleId}] 与 [{b.RuleId}] 完全重复（{a.MatchType}/{a.MatchMode}/{a.MatchValue}），请合并或删除其一"));
                    continue;
                }

                if (RangesOverlap(a, b, out var detail))
                {
                    issues.Add(Issue(
                        ErrorCodes.ConfigValidationFailed,
                        $"规则 [{a.RuleId}] 与 [{b.RuleId}] 匹配范围重叠（{detail}）：同时命中时按列表顺序先者优先（warning）"));
                }
            }
        }

        return issues;
    }

    private static void ValidateDisplayLines(IReadOnlyList<DisplayLine> lines, string section, List<ConfigIssue> issues)
    {
        if (lines is null || lines.Count == 0)
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"节 {section} 至少需一行文字"));
            return;
        }

        foreach (var (line, index) in lines.Select((l, i) => (l, i)))
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"{section}[{index}].text 不能为空"));
            }

            if (line.FontSize < 0)
            {
                issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"{section}[{index}].fontSize 不能为负数"));
            }
        }
    }

    private static void ValidateDuration(double seconds, string field, List<ConfigIssue> issues)
    {
        if (seconds < 0 || seconds > ConfigLoader.MaxDurationSeconds)
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"{field} {seconds} 超出合法范围 [0, {ConfigLoader.MaxDurationSeconds}]"));
        }
    }

    private static void ValidateRule(RuleConfig rule, List<ConfigIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(rule.RuleId))
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, "规则 ruleId 不能为空"));
        }

        if (string.IsNullOrWhiteSpace(rule.MatchValue))
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"规则 [{rule.RuleId}] 匹配值 matchValue 不能为空"));
        }

        if (rule.MatchMode == MatchMode.Wildcard)
        {
            if (!WildcardPatternRegex.IsMatch(rule.MatchValue))
            {
                issues.Add(Issue(
                    ErrorCodes.ConfigValidationFailed,
                    $"规则 [{rule.RuleId}] 通配符语法非法（仅支持 * 与 ?，不得含 [ ] ( ) {{ }} ^ $ + |）：{rule.MatchValue}"));
            }
            else if (!rule.MatchValue.Contains('*') && !rule.MatchValue.Contains('?'))
            {
                issues.Add(Issue(
                    ErrorCodes.ConfigValidationFailed,
                    $"规则 [{rule.RuleId}] 通配模式（wildcard）下匹配值应至少含一个 * 或 ?"));
            }
        }

        ValidateDuration(rule.DelaySeconds, $"规则 [{rule.RuleId}] delaySeconds", issues);
        ValidateDuration(rule.HoldSeconds, $"规则 [{rule.RuleId}] holdSeconds", issues);

        if (!string.IsNullOrEmpty(rule.OutlineColor) && !HexColorRegex.IsMatch(rule.OutlineColor))
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"规则 [{rule.RuleId}] 描边色非法（需 #RRGGBB 或 #AARRGGBB）：{rule.OutlineColor}"));
        }

        if (!string.IsNullOrWhiteSpace(rule.OutlineMode) && !IsValidOutlineMode(rule.OutlineMode))
        {
            issues.Add(Issue(ErrorCodes.ConfigValidationFailed, $"规则 [{rule.RuleId}] outlineMode 非法（需 shadow / stroke）：{rule.OutlineMode}"));
        }

        ValidateDisplayLines(rule.DisplayLines, $"rules[{rule.RuleId}].displayLines", issues);
    }

    /// <summary>两规则匹配范围是否可能重叠（不依赖具体目标文本的保守判定）。</summary>
    private static bool RangesOverlap(RuleConfig a, RuleConfig b, out string detail)
    {
        // 通配模式转受控正则（shared 语义：* → .*、? → .）
        string? Pattern(RuleConfig r) => r.MatchMode switch
        {
            MatchMode.Wildcard when WildcardPatternRegex.IsMatch(r.MatchValue) =>
                "^" + Regex.Escape(r.MatchValue).Replace("\\*", ".*").Replace("\\?", ".") + "$",
            _ => null,
        };

        var pa = Pattern(a);
        var pb = Pattern(b);
        var cmp = Comparison(a) & Comparison(b);

        // 场景 1：exact/contains 值被对方通配模式覆盖
        if (pa is not null && (b.MatchMode is MatchMode.Exact or MatchMode.Contains) &&
            Regex.IsMatch(b.MatchValue, pa, RegexOptions.None))
        {
            detail = $"匹配值 [{b.MatchValue}] 被通配模式 [{a.MatchValue}] 覆盖";
            return true;
        }

        if (pb is not null && (a.MatchMode is MatchMode.Exact or MatchMode.Contains) &&
            Regex.IsMatch(a.MatchValue, pb, RegexOptions.None))
        {
            detail = $"匹配值 [{a.MatchValue}] 被通配模式 [{b.MatchValue}] 覆盖";
            return true;
        }

        // 场景 2：包含关系（exact 目标与 contains/contains 值存在包含）
        if (a.MatchMode is MatchMode.Contains && b.MatchMode is MatchMode.Contains)
        {
            var contains = a.MatchValue.Contains(b.MatchValue, cmp) || b.MatchValue.Contains(a.MatchValue, cmp);
            if (contains)
            {
                detail = $"包含模式 [{a.MatchValue}] 与 [{b.MatchValue}] 互为包含";
                return true;
            }
        }

        // 场景 3：两个通配模式——保守判定仅当一方字面文本落入另一方模式
        if (pa is not null && pb is not null)
        {
            var literalA = StripWildcards(a.MatchValue);
            var literalB = StripWildcards(b.MatchValue);
            if (literalA.Length > 0 && Regex.IsMatch(literalA, pb, RegexOptions.None) ||
                literalB.Length > 0 && Regex.IsMatch(literalB, pa, RegexOptions.None))
            {
                detail = $"通配模式 [{a.MatchValue}] 与 [{b.MatchValue}] 存在可同时命中的目标";
                return true;
            }
        }

        detail = string.Empty;
        return false;
    }

    private static string StripWildcards(string value) => value.Replace("*", string.Empty).Replace("?", string.Empty);

    /// <summary>描边方式取值合法性（架构 §3.3）：shadow / stroke（大小写不敏感）。</summary>
    private static bool IsValidOutlineMode(string? mode)
        => !string.IsNullOrWhiteSpace(mode)
            && (string.Equals(mode.Trim(), "shadow", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode.Trim(), "stroke", StringComparison.OrdinalIgnoreCase));

    private static StringComparison Comparison(RuleConfig rule) =>
        rule.MatchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static ConfigIssue Issue(string code, string message) => new() { ErrorCode = code, Message = message };
}