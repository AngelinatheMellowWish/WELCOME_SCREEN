using Object1688.Shared.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 手动大字配置节（架构 §5.7 manual，F-12：手动触发大字独立完整样式）。
/// 对应 config.json manual 对象。
/// </summary>
public sealed class ManualConfig
{
    /// <summary>手动大字是否启用。缺省 true。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>多行段落模型（F-06）。至少一行。</summary>
    public required IReadOnlyList<DisplayLine> DisplayLines { get; init; }

    /// <summary>触发后延时显示秒数。缺省 0。</summary>
    public double DelaySeconds { get; init; }

    /// <summary>保持显示秒数。缺省 4。</summary>
    public double HoldSeconds { get; init; } = 4;

    /// <summary>换行策略。缺省 Wrap。</summary>
    public WrapStrategy WrapStrategy { get; init; } = WrapStrategy.Wrap;

    /// <summary>手动大字独立位置（center | top-center | custom）。缺省 center。</summary>
    public string Position { get; init; } = "center";

    /// <summary>手动大字独立字号（覆盖各行缺省，无则用全局 defaultFontSize）。缺省 96。</summary>
    public double FontSize { get; init; } = 96;

    /// <summary>手动大字对齐方式。缺省 Center。</summary>
    public TextAlignment Align { get; init; } = TextAlignment.Center;

    /// <summary>独立描边色（留空 = 全局 defaultOutlineColor）。</summary>
    public string OutlineColor { get; init; } = string.Empty;

    /// <summary>独立描边宽（-1 = 跟随全局 defaultOutlineWidth）。</summary>
    public double OutlineWidth { get; init; } = -1;

    /// <summary>独立描边方式（shadow/stroke）。空串 = 跟随全局 outlineMode。</summary>
    public string OutlineMode { get; init; } = string.Empty;

    /// <summary>手动大字目标屏（独立，NFR-05）。"primary" 或显示器索引号。</summary>
    public string TargetScreen { get; init; } = "primary";

    /// <summary>全局热键（可改/可禁用 null，F-12）。缺省 Alt+F。</summary>
    public string? Shortcut { get; init; } = "Alt+F";
}