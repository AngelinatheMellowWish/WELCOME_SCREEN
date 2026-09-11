namespace Object1688.Shared.Text;

/// <summary>
/// 样式预设参数集（架构 §5.6 样式预设：参数值仅作起点，应用后仍可微调，数值随 M2 视觉验收可调）。
/// 预设内容与架构 §5.6 表格逐条一致。
/// </summary>
public sealed record StylePreset
{
    /// <summary>预设标识（stable id，存配置时使用）。</summary>
    public required string Id { get; init; }

    /// <summary>预设显示名（UI 展示，走 i18n）。</summary>
    public required string DisplayName { get; init; }

    /// <summary>字号（DIP）。</summary>
    public required double FontSize { get; init; }

    /// <summary>文字颜色（#RRGGBB）。</summary>
    public required string Color { get; init; }

    /// <summary>描边色（#RRGGBB；空 = 无描边）。</summary>
    public required string OutlineColor { get; init; }

    /// <summary>描边宽度（DIP）。</summary>
    public required double OutlineWidth { get; init; }

    /// <summary>位置（center | top-center | custom）。</summary>
    public required string Position { get; init; }
}

/// <summary>
/// 内置样式预设集合（架构 §5.6 表：AC-60/AC-80 交付物）。
/// </summary>
public static class StylePresets
{
    /// <summary>Control 经典（默认风格）。</summary>
    public static readonly StylePreset ControlClassic = new()
    {
        Id = "control-classic",
        DisplayName = "control-classic（Control 经典）",
        FontSize = 120,
        Color = "#FFFFFF",
        OutlineColor = "",
        OutlineWidth = 0,
        Position = "center",
    };

    /// <summary>高对比（防白底撞色）。</summary>
    public static readonly StylePreset ControlHighContrast = new()
    {
        Id = "control-highcontrast",
        DisplayName = "control-highcontrast（高对比）",
        FontSize = 120,
        Color = "#FFFFFF",
        OutlineColor = "#000000",
        OutlineWidth = 4,
        Position = "center",
    };

    /// <summary>字幕风·浅（偏上显示）。</summary>
    public static readonly StylePreset SubtitleLight = new()
    {
        Id = "subtitle-light",
        DisplayName = "subtitle-light（字幕风·浅）",
        FontSize = 96,
        Color = "#FFFFFF",
        OutlineColor = "#000000",
        OutlineWidth = 3,
        Position = "top-center",
    };

    /// <summary>影院·深底（亮底场景醒目）。</summary>
    public static readonly StylePreset CinemaDark = new()
    {
        Id = "cinema-dark",
        DisplayName = "cinema-dark（影院·深底）",
        FontSize = 110,
        Color = "#000000",
        OutlineColor = "#FFFFFF",
        OutlineWidth = 3,
        Position = "center",
    };

    /// <summary>全部预设（顺序即 UI 展示顺序）。</summary>
    public static IReadOnlyList<StylePreset> All { get; } = new[]
    {
        ControlClassic,
        ControlHighContrast,
        SubtitleLight,
        CinemaDark,
    };

    /// <summary>
    /// 按 Id 查找预设；未知 Id 返回 Null。
    /// </summary>
    public static StylePreset? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var preset in All)
        {
            if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return null;
    }

    /// <summary>
    /// 校验 Id 是否属于内置预设（未知 Id 保存时应提示，对应 AC-60 预设选择校验）。
    /// </summary>
    public static bool IsKnown(string? id) => FindById(id) is not null;
}