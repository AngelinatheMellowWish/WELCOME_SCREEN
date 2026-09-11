namespace Object1688.Shared.Text;

/// <summary>
/// 大字多行段落模型中的单行（架构 §4.2 displayLines 元素）。
/// 每行独立文字/字号/颜色/描边/对齐；缺省字段回退到块级或全局默认。
/// </summary>
public sealed class DisplayLine
{
    /// <summary>行文字（支持 {appName}/{time} 占位符，F-04/F-06）。</summary>
    public required string Text { get; init; }

    /// <summary>行字号（设备无关像素，DIP）。0 或负数表示使用块级/全局默认字号。</summary>
    public double FontSize { get; init; }

    /// <summary>行文字颜色（#RRGGBB 或 #AARRGGBB）。空串 = 块级/全局默认。</summary>
    public string? Color { get; init; }

    /// <summary>行独立描边色（F-06 富文本扩展）。空串/Null = 回退块级描边色。</summary>
    public string? OutlineColor { get; init; }

    /// <summary>行独立描边宽度（DIP）。-1 = 回退块级描边宽。</summary>
    public double OutlineWidth { get; init; } = -1;

    /// <summary>行对齐（缺省 = Center）。</summary>
    public TextAlignment Align { get; init; } = TextAlignment.Center;
}