using System.Text;

namespace Object1688.Shared.Text;

/// <summary>
/// 单行排版结果（BannerMetrics.Calculate 输出，供渲染层直接使用）。
/// </summary>
public sealed class BannerLineMetrics
{
    /// <summary>解析占位符后的最终文字。</summary>
    public required string Text { get; init; }

    /// <summary>最终字号（DIP；Shrink 策略下可能小于请求值）。</summary>
    public required double FontSize { get; init; }

    /// <summary>行宽（DIP）。</summary>
    public required double Width { get; init; }

    /// <summary>行高（DIP，按字号比例推定）。</summary>
    public required double Height { get; init; }

    /// <summary>渲染颜色（#RRGGBB）。</summary>
    public required string Color { get; init; }

    /// <summary>描边色（空 = 无描边）。</summary>
    public required string OutlineColor { get; init; }

    /// <summary>描边宽度（DIP）。</summary>
    public required double OutlineWidth { get; init; }

    /// <summary>行对齐。</summary>
    public required TextAlignment Align { get; init; }
}

/// <summary>
/// 整块大字的排版结果。
/// </summary>
public sealed class BannerMetricsResult
{
    /// <summary>各行排版结果（按显示顺序）。</summary>
    public required IReadOnlyList<BannerLineMetrics> Lines { get; init; }

    /// <summary>块总宽（DIP，最宽行）。</summary>
    public required double BlockWidth { get; init; }

    /// <summary>块总高（DIP，各行高之和 + 行间距）。</summary>
    public required double BlockHeight { get; init; }

    /// <summary>实际行间距（DIP，字号 × 行间距系数）。</summary>
    public required double LineGap { get; init; }

    /// <summary>Shrink 策略生效时的缩放因子（1 = 未缩放）。</summary>
    public required double ShrinkFactor { get; init; }
}

/// <summary>
/// 大字排版计算引擎（架构 §3.2 多行段落模型 / 架构 §4.2 wrapStrategy）。
/// 纯 C#：文本测量经 <see cref="TextMeasure"/> 委托注入（WPF 侧用 FormattedText，单测用伪测量），
/// 因此本类可在无 UI 线程环境下单元测试。
/// </summary>
public static class BannerMetrics
{
    /// <summary>行间距系数（行高 × 系数）。Control 风格大字行距紧凑。</summary>
    public const double LineGapFactor = 0.35;

    /// <summary>Shrink 策略最小字号占比（相对行请求字号，防止过度缩到不可读）。</summary>
    public const double MinShrinkRatio = 0.4;

    /// <summary>文本测量委托：给定文本与字号（DIP），返回文本宽度（DIP）。</summary>
    public delegate double TextMeasure(string text, double fontSize);

    /// <summary>
    /// 计算整块大字排版。
    /// </summary>
    /// <param name="request">渲染请求（DisplayLines 与块级默认已合并）。</param>
    /// <param name="screenWidth">可用屏宽（DIP，工作区）。</param>
    /// <param name="measure">文本测量委托。</param>
    /// <param name="maxWidthRatio">Wrap 策略下文本允许占用屏宽的最大比例（0~1，默认 0.9）。</param>
    public static BannerMetricsResult Calculate(BannerRequest request, double screenWidth, TextMeasure measure, double maxWidthRatio = 0.9)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(measure);

        if (request.DisplayLines.Count == 0)
        {
            throw new ArgumentException("DisplayLines 至少需要一行", nameof(request));
        }

        var maxLineWidth = screenWidth * Math.Clamp(maxWidthRatio, 0.2, 1.0);

        // 1) 解析各行请求字号与占位符
        var rawLines = new List<(string Text, double FontSize, string Color, string OutlineColor, double OutlineWidth, TextAlignment Align)>();
        foreach (var line in request.DisplayLines)
        {
            var resolved = PlaceholderResolver.Resolve(line.Text, request.AppName, request.TriggerTime, request.TimeFormat);

            // 拆 \n 为独立行（Manual 手动换行；Wrap/Shrink 也接受显式换行）
            if (resolved.Contains('\n'))
            {
                foreach (var seg in resolved.Split('\n'))
                {
                    rawLines.Add((seg,
                        line.FontSize > 0 ? line.FontSize : request.FontSize,
                        ResolveColor(line.Color, request.Color),
                        ResolveOutlineColor(line.OutlineColor, request.OutlineColor),
                        ResolveOutlineWidth(line.OutlineWidth, request.OutlineWidth, line.OutlineColor, request.OutlineColor),
                        line.Align));
                }
            }
            else
            {
                rawLines.Add((resolved,
                    line.FontSize > 0 ? line.FontSize : request.FontSize,
                    ResolveColor(line.Color, request.Color),
                    ResolveOutlineColor(line.OutlineColor, request.OutlineColor),
                    ResolveOutlineWidth(line.OutlineWidth, request.OutlineWidth, line.OutlineColor, request.OutlineColor),
                    line.Align));
            }
        }

        // 2) 按换行策略处理
        var strategy = request.WrapStrategy;

        // 3) Wrap：超出可用宽的整行按单词/字符折行
        if (strategy == WrapStrategy.Wrap)
        {
            var wrapped = new List<(string, double, string, string, double, TextAlignment)>();
            foreach (var (text, fs, color, oc, ow, align) in rawLines)
            {
                AppendWrapped(wrapped, text, fs, color, oc, ow, align, maxLineWidth, measure);
            }

            rawLines = wrapped;
        }

        // 4) Shrink：不换行，整体缩放字号适配最宽行
        double shrinkFactor = 1.0;
        if (strategy == WrapStrategy.Shrink && rawLines.Count > 0)
        {
            double maxRawWidth = 0;
            double maxFont = 0;
            foreach (var (text, fs, _, _, _, _) in rawLines)
            {
                var w = measure(text, fs);
                if (w > maxRawWidth)
                {
                    maxRawWidth = w;
                    maxFont = fs;
                }
            }

            if (maxRawWidth > maxLineWidth && maxRawWidth > 0)
            {
                var candidate = maxLineWidth / maxRawWidth;
                shrinkFactor = Math.Max(candidate, MinShrinkRatio);
            }
        }

        // 5) 计算最终各行尺寸
        var lines = new List<BannerLineMetrics>(rawLines.Count);
        double blockWidth = 0;
        double blockHeight = 0;
        foreach (var (text, fs, color, oc, ow, align) in rawLines)
        {
            if (string.IsNullOrEmpty(text))
            {
                // 空行（手动换行产生）：保留行间距高度
                var gapH = fs * LineGapFactor;
                lines.Add(new BannerLineMetrics { Text = string.Empty, FontSize = fs, Width = 0, Height = gapH, Color = color, OutlineColor = oc, OutlineWidth = ow, Align = align });
                blockHeight += gapH;
                continue;
            }

            var fontSize = shrinkFactor < 1.0 ? fs * shrinkFactor : fs;
            var width = measure(text, fontSize);
            var height = fontSize * (1 + LineGapFactor);

            lines.Add(new BannerLineMetrics
            {
                Text = text,
                FontSize = fontSize,
                Width = width,
                Height = height,
                Color = color,
                OutlineColor = oc,
                OutlineWidth = ow,
                Align = align,
            });

            if (width > blockWidth)
            {
                blockWidth = width;
            }

            blockHeight += height;
        }

        return new BannerMetricsResult
        {
            Lines = lines,
            BlockWidth = blockWidth,
            BlockHeight = blockHeight,
            LineGap = LineGapFactor,
            ShrinkFactor = shrinkFactor,
        };
    }

    /// <summary>将超长文本按空白/字符折行追加到目标列表（Wrap 策略；支持 CJK 无空白文本按字符硬折）。</summary>
    private static void AppendWrapped(
        List<(string, double, string, string, double, TextAlignment)> target,
        string text, double fontSize, string color, string outlineColor, double outlineWidth, TextAlignment align,
        double maxWidth, TextMeasure measure)
    {
        if (measure(text, fontSize) <= maxWidth)
        {
            target.Add((text, fontSize, color, outlineColor, outlineWidth, align));
            return;
        }

        // 合并空白后按词切分；词本身超宽（如 CJK 无空白文本）则按字符硬折
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (measure(word, fontSize) > maxWidth)
            {
                AppendCharsWrapped(target, word, fontSize, color, outlineColor, outlineWidth, align, maxWidth, measure);
                continue;
            }

            // 尝试并入当前最后一行；放不下则另起一行
            if (target.Count > 0)
            {
                var (t, fs, c, oc, ow, a) = target[^1];
                var combined = string.IsNullOrEmpty(t) ? word : t + " " + word;
                if (measure(combined, fontSize) <= maxWidth)
                {
                    target[^1] = (combined, fs, c, oc, ow, a);
                    continue;
                }
            }

            target.Add((word, fontSize, color, outlineColor, outlineWidth, align));
        }
    }

    /// <summary>单个超宽词按字符硬折为多行（每行宽度 ≤ maxWidth）。</summary>
    private static void AppendCharsWrapped(
        List<(string, double, string, string, double, TextAlignment)> target,
        string word, double fontSize, string color, string outlineColor, double outlineWidth, TextAlignment align,
        double maxWidth, TextMeasure measure)
    {
        var line = new StringBuilder();
        foreach (var ch in word)
        {
            var candidate = line.Length == 0 ? ch.ToString() : line + ch.ToString();
            if (line.Length > 0 && measure(candidate, fontSize) > maxWidth)
            {
                target.Add((line.ToString(), fontSize, color, outlineColor, outlineWidth, align));
                line.Clear();
            }

            line.Append(ch);
        }

        if (line.Length > 0)
        {
            target.Add((line.ToString(), fontSize, color, outlineColor, outlineWidth, align));
        }
    }

    private static string ResolveColor(string? lineColor, string blockColor)
        => string.IsNullOrWhiteSpace(lineColor) ? NormalizeHex(blockColor) : NormalizeHex(lineColor);

    private static string ResolveOutlineColor(string? lineOutline, string blockOutline)
        => string.IsNullOrWhiteSpace(lineOutline) ? NormalizeHex(blockOutline) : NormalizeHex(lineOutline);

    /// <summary>行描边宽度：行显式给出则用；否则行有描边色继承块级宽，行无描边色且块级宽=0 则为 0。</summary>
    private static double ResolveOutlineWidth(double lineWidth, double blockWidth, string? lineOutline, string blockOutline)
    {
        if (lineWidth >= 0)
        {
            return lineWidth;
        }

        if (!string.IsNullOrWhiteSpace(lineOutline))
        {
            // 行显式指定了描边色 → 继承块级宽度（可能为 0 → 有描边色但宽 0，等同无描边）
            return Math.Max(0, blockWidth);
        }

        return Math.Max(0, blockWidth);
    }

    /// <summary>规范化十六进制颜色：#RGB → #RRGGBB；带 #AARRGGBB 保持；非法输入原样返回（渲染层容错）。</summary>
    public static string NormalizeHex(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return "#FFFFFF";
        }

        var c = color.Trim();
        if (c.StartsWith('#') && c.Length == 4)
        {
            return $"#{c[1]}{c[1]}{c[2]}{c[2]}{c[3]}{c[3]}";
        }

        return c;
    }
}