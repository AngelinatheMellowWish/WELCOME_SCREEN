namespace Object1688.Shared.Text;

/// <summary>
/// 一次大字显示请求（Main→Overlay TriggerCommand 的载荷 / ConfigUI 测试显示）。
/// 对应架构 §4.2 规则结构 + §5.7 全局默认的合并语义快照：Overlay 渲染前已由 Main/ConfigUI 完成
/// 「规则级字段 → 全局默认」逐项合并，本请求只含最终渲染参数。
/// </summary>
public sealed class BannerRequest
{
    /// <summary>多行段落（F-06：每行独立文字/字号/颜色/描边/对齐）。至少一行。</summary>
    public required IReadOnlyList<DisplayLine> DisplayLines { get; init; }

    /// <summary>块级默认字号（DIP）；行未指定 FontSize 时使用。默认 96（全局 defaultFontSize）。</summary>
    public double FontSize { get; init; } = 96;

    /// <summary>块级默认文字颜色（#RRGGBB）。默认纯白。</summary>
    public string Color { get; init; } = "#FFFFFF";

    /// <summary>块级描边色。默认黑。</summary>
    public string OutlineColor { get; init; } = "#000000";

    /// <summary>块级描边宽度（DIP）。0 = 无描边（默认）。</summary>
    public double OutlineWidth { get; init; }

    /// <summary>描边渲染方式（架构 §3.3）。缺省 <see cref="OutlineMode.Shadow"/>（方案 A 柔光）。</summary>
    public OutlineMode OutlineMode { get; init; } = OutlineMode.Shadow;

    /// <summary>块级对齐。默认 Center。</summary>
    public TextAlignment Align { get; init; } = TextAlignment.Center;

    /// <summary>换行策略（架构 §4.2 wrapStrategy）。默认 Wrap。</summary>
    public WrapStrategy WrapStrategy { get; init; } = WrapStrategy.Wrap;

    /// <summary>触发后延时显示秒数（架构 §5.7：欢迎 delaySeconds 默认 1，规则默认 2）。</summary>
    public double DelaySeconds { get; init; }

    /// <summary>目标显示器（架构 §4.2 targetScreen）："primary"（默认主屏）或显示器索引号（字符串化）。</summary>
    public string TargetScreen { get; init; } = "primary";

    /// <summary>{time} 占位符格式（架构 §5.7 global.timeFormat；M2 阶段配置未接入时为默认 "auto"）。</summary>
    public string TimeFormat { get; init; } = "auto";

    /// <summary>浮现动画时长（毫秒，默认 400，架构 §3.2 300~500）。</summary>
    public int FadeInMs { get; init; } = 400;

    /// <summary>保持时长（秒，默认 4）。</summary>
    public double HoldSeconds { get; init; } = 4;

    /// <summary>淡出动画时长（毫秒，默认 600，架构 §3.2 500~1000）。</summary>
    public int FadeOutMs { get; init; } = 600;

    /// <summary>大字块位置（架构 §4.2 position：center | top-center | custom {x,y}）。</summary>
    public string Position { get; init; } = "center";

    /// <summary>自定义位置（Position="custom" 时生效；工作区归一化坐标 0~1）。</summary>
    public double? CustomX { get; init; }

    /// <summary>自定义位置（Position="custom" 时生效；工作区归一化坐标 0~1）。</summary>
    public double? CustomY { get; init; }

    /// <summary>占位符上下文：{appName} 替换值（本次命中进程名/窗口标题；可为空则保留原文）。</summary>
    public string? AppName { get; init; }

    /// <summary>占位符上下文：{time} 触发时刻（默认当前时间）。</summary>
    public DateTimeOffset? TriggerTime { get; init; }

    /// <summary>提示音请求（F-08）：true 请示播放（由全局/规则判定后传入）。默认关。</summary>
    public bool PlaySound { get; init; }
}