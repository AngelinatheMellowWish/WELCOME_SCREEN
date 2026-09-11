using Object1688.Shared.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 单条触发规则（架构 §4.2 匹配规则结构）。
/// 对应 config.json rules[] 元素；反序列化用 JSON 选项（camelCase + 枚举转字符串）。
/// 向后兼容（§4.2 注）：旧的单行 <c>displayText</c> 字段由配置加载器映射为单元素
/// <see cref="DisplayLines"/>；<see cref="MatchMode"/> 默认 <see cref="MatchMode.Exact"/>、
/// <see cref="Enabled"/> 默认 true、<see cref="MinIntervalSeconds"/> / <see cref="SoundEnabled"/>
/// 缺省按 null 处理（跟随全局）。
/// </summary>
public sealed class RuleConfig
{
    /// <summary>规则唯一标识（如 r-001）。</summary>
    public required string RuleId { get; init; }

    /// <summary>匹配目标类型：进程名 / 窗口标题。</summary>
    public required MatchType MatchType { get; init; }

    /// <summary>匹配值（进程名如 chrome.exe，或窗口标题文本/通配模式）。</summary>
    public required string MatchValue { get; init; }

    /// <summary>匹配模式（exact/contains/wildcard）。缺省 exact。</summary>
    public MatchMode MatchMode { get; init; } = MatchMode.Exact;

    /// <summary>规则启用/禁用开关（F-20）。false 时跳过匹配，配置保留。缺省 true。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>匹配是否区分大小写。缺省 false（不区分）。</summary>
    public bool MatchCaseSensitive { get; init; }

    /// <summary>全屏应用亦触发（AC-05）。缺省 true。</summary>
    public bool IncludeFullscreen { get; init; } = true;

    /// <summary>
    /// 目标显示器（"primary" 或显示器索引号）。
    /// 空串 = 未配置，合并时跟随全局 <c>global.targetScreen</c>（§5.7）。
    /// </summary>
    public string TargetScreen { get; init; } = "";

    /// <summary>多行段落模型（F-06，每行独立文字/字号/颜色/描边/对齐）。至少一行。</summary>
    public required IReadOnlyList<DisplayLine> DisplayLines { get; init; }

    /// <summary>换行策略：wrap/shrink/manual。缺省 Wrap。</summary>
    public WrapStrategy WrapStrategy { get; init; } = WrapStrategy.Wrap;

    /// <summary>触发后延时显示秒数。缺省 2（架构 §4.2 注/§5.7）。</summary>
    public double DelaySeconds { get; init; } = 2;

    /// <summary>保持显示秒数。缺省 4。</summary>
    public double HoldSeconds { get; init; } = 4;

    /// <summary>
    /// 大字块位置（center | top-center | custom {x,y}）。
    /// 空串 = 未配置，合并时跟随全局 <c>global.defaultPosition</c>（§5.7）。
    /// </summary>
    public string Position { get; init; } = "";

    /// <summary>
    /// 块级描边色（#RRGGBB）。
    /// 空串 = 未配置，合并时跟随全局 <c>global.defaultOutlineColor</c>（§5.7）。
    /// </summary>
    public string OutlineColor { get; init; } = "";

    /// <summary>
    /// 块级描边宽度（DIP）。0 = 无描边。
    /// 负值（-1）= 未配置，合并时跟随全局 <c>global.defaultOutlineWidth</c>（§5.7）。
    /// </summary>
    public double OutlineWidth { get; init; } = -1;

    /// <summary>
    /// 规则级描边方式（架构 §3.3）：shadow=方案 A 柔光 / stroke=方案 B 精确。
    /// 空串 = 未配置，合并时跟随全局 <c>global.outlineMode</c>。
    /// </summary>
    public string OutlineMode { get; init; } = "";

    /// <summary>
    /// 规则级最小触发间隔（秒，F-10 扩展，架构 §4.4）。
    /// null = 用全局 <c>minTriggerIntervalSeconds</c> 默认；0 = 不限。
    /// </summary>
    public double? MinIntervalSeconds { get; init; }

    /// <summary>规则级提示音开关（F-08 扩展）。null = 跟随全局 sound.enabled。</summary>
    public bool? SoundEnabled { get; init; }
}