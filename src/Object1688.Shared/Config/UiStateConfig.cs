namespace Object1688.Shared.Config;

/// <summary>
/// 窗口布局记忆节（架构 §5.7 uiState / AC-61 / F-30）。
/// 配置/性能窗口的位置/大小/分栏/页签/时间窗持久化；窗口关闭写回 uiState → ConfigChanged 保存。
/// 字段可空：未写回的维度在下次窗口打开时使用默认布局。
/// </summary>
public sealed class UiStateConfig
{
    /// <summary>配置窗口布局。</summary>
    public UiWindowLayout? ConfigWin { get; init; }

    /// <summary>性能窗口布局。</summary>
    public UiWindowLayout? PerfWin { get; init; }
}

/// <summary>单个窗口的布局记忆。</summary>
public sealed class UiWindowLayout
{
    /// <summary>窗口 X（屏幕坐标；null = 居中）。</summary>
    public double? X { get; init; }

    /// <summary>窗口 Y（屏幕坐标；null = 居中）。</summary>
    public double? Y { get; init; }

    /// <summary>窗口宽（DIP）。</summary>
    public double? Width { get; init; }

    /// <summary>窗口高（DIP）。</summary>
    public double? Height { get; init; }

    /// <summary>分栏宽（规则列表/主区分隔；像素）。</summary>
    public double? Splitter { get; init; }

    /// <summary>当前页签（如 "rules"、"welcome"、"manual"、"dnd"、"overview"）。</summary>
    public string? Tab { get; init; }

    /// <summary>性能窗口时间窗档位（"60s" / "5min"；仅性能窗口使用）。</summary>
    public string? TimeWindow { get; init; }
}
