using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Object1688.Shared.Text;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 大字视觉树（BannerVisualFactory.Build 输出）。
/// <see cref="Root"/> 铺满目标屏工作区并完成块定位；<see cref="Block"/> 为可动画元素
/// （浮现/淡出作用于块，缩放以块为中心——top-center/custom 定位下缩放原点正确）。
/// 描边（DropShadowEffect）引用被登记，供帧率降级（AC-68）剥离/恢复。
/// </summary>
public sealed class BannerVisual
{
    /// <summary>铺满工作区的定位根（窗口内容）。</summary>
    public required Grid Root { get; init; }

    /// <summary>整块大字容器（动画目标）。</summary>
    public required StackPanel Block { get; init; }

    private readonly List<(TextBlock Block, Effect? Effect)> _outlines = new();

    internal void RegisterOutline(TextBlock textBlock, Effect? effect) => _outlines.Add((textBlock, effect));

    /// <summary>剥离全部描边特效（帧率低于阈值时的降档，AC-67/68）。</summary>
    public void StripEffects()
    {
        foreach (var (block, _) in _outlines)
        {
            block.Effect = null;
        }
    }

    /// <summary>恢复描边特效（帧率回升持续 N 秒后自动回全特效，AC-68）。</summary>
    public void RestoreEffects()
    {
        foreach (var (block, effect) in _outlines)
        {
            block.Effect = effect;
        }
    }
}

/// <summary>
/// 大字视觉构建器（架构 §3.2 多行段落模型 / §3.3 描边方案 A）。
/// 将 BannerMetricsResult 排版结果转为 WPF 视觉树：每行独立 TextBlock（字号/色/描边/对齐），
/// 描边采用 DropShadowEffect（方案 A）：ShadowDepth=0 的纯色阴影模拟描边。
/// </summary>
public static class BannerVisualFactory
{
    /// <summary>
    /// 构建整块大字视觉树。
    /// </summary>
    /// <param name="request">渲染请求（position/custom 用于定位）。</param>
    /// <param name="metrics">排版结果。</param>
    /// <param name="workArea">目标屏工作区（DIP），用于 top-center/custom 定位。</param>
    public static BannerVisual Build(BannerRequest request, BannerMetricsResult metrics, ScreenWorkArea workArea)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metrics);

        var block = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var visual = new BannerVisual { Root = new Grid(), Block = block };
        foreach (var line in metrics.Lines)
        {
            block.Children.Add(BuildLine(line, metrics.BlockWidth, request.OutlineMode, visual));
        }

        // 定位（架构 §4.2 position；坐标基于目标屏工作区 AC-48）
        var root = visual.Root;
        root.Width = workArea.Width;
        root.Height = workArea.Height;

        switch (request.Position.Trim().ToLowerInvariant())
        {
            case "top-center":
                block.VerticalAlignment = VerticalAlignment.Top;
                block.Margin = new Thickness(0, workArea.Height * 0.08, 0, 0);
                break;

            case "custom":
            {
                // 归一化坐标（0~1）→ 工作区像素；块中心对齐该点，越界时内夹回工作区
                var x = Math.Clamp(request.CustomX ?? 0.5, 0.0, 1.0);
                var y = Math.Clamp(request.CustomY ?? 0.5, 0.0, 1.0);
                var left = Math.Clamp(x * workArea.Width - metrics.BlockWidth / 2, 0, Math.Max(0, workArea.Width - metrics.BlockWidth));
                var top = Math.Clamp(y * workArea.Height - metrics.BlockHeight / 2, 0, Math.Max(0, workArea.Height - metrics.BlockHeight));
                block.HorizontalAlignment = HorizontalAlignment.Left;
                block.VerticalAlignment = VerticalAlignment.Top;
                block.Margin = new Thickness(left, top, 0, 0);
                break;
            }

            default: // center
                block.HorizontalAlignment = HorizontalAlignment.Center;
                block.VerticalAlignment = VerticalAlignment.Center;
                break;
        }

        root.Children.Add(block);
        return visual;
    }

    /// <summary>
    /// 构建单行视觉元素：字号/颜色/对齐，块宽为排版块宽使对齐生效。
    /// 描边按 <see cref="OutlineMode"/> 选择实现：
    /// Shadow = 方案 A（DropShadowEffect 柔光晕）；Stroke = 方案 B（8 向偏移实心复制，精确描边）。
    /// 描边元素登记到 <paramref name="visual"/>，供帧率降级剥离/恢复（AC-68）。
    /// </summary>
    private static FrameworkElement BuildLine(BannerLineMetrics line, double blockWidth, OutlineMode mode, BannerVisual visual)
    {
        var fillBrush = FontAssets.ParseColor(line.Color, "#FFFFFF");
        if (line.OutlineWidth <= 0 || string.IsNullOrWhiteSpace(line.OutlineColor))
        {
            return CreateTextBlock(line, blockWidth, fillBrush);
        }

        var outlineBrush = FontAssets.ParseColor(line.OutlineColor, "#000000");
        if (mode == OutlineMode.Stroke)
        {
            return BuildStrokeLine(line, blockWidth, outlineBrush, fillBrush, visual);
        }

        // 方案 A：单个 DropShadowEffect（ShadowDepth=0 的纯色阴影模拟柔光描边）
        var textBlock = CreateTextBlock(line, blockWidth, fillBrush);
        var effect = new DropShadowEffect
        {
            Color = outlineBrush.Color,
            ShadowDepth = 0,
            BlurRadius = Math.Max(0.5, line.OutlineWidth),
            Opacity = 1.0,
        };
        textBlock.Effect = effect;
        visual.RegisterOutline(textBlock, effect);
        return textBlock;
    }

    /// <summary>方案 B：8 向偏移实心复制叠加于填充文字之下，形成精确（实心）描边。</summary>
    private static FrameworkElement BuildStrokeLine(
        BannerLineMetrics line, double blockWidth, SolidColorBrush outlineBrush, Brush fillBrush, BannerVisual visual)
    {
        var grid = new Grid { Width = blockWidth, Height = line.Height };
        foreach (var direction in StrokeDirections)
        {
            var effect = new DropShadowEffect
            {
                Color = outlineBrush.Color,
                ShadowDepth = line.OutlineWidth,
                Direction = direction,
                BlurRadius = 0, // 0 = 无模糊，偏移复制为实心 → 精确描边
                Opacity = 1.0,
            };
            var copy = CreateTextBlock(line, blockWidth, outlineBrush);
            copy.Effect = effect;
            visual.RegisterOutline(copy, effect);
            grid.Children.Add(copy);
        }

        grid.Children.Add(CreateTextBlock(line, blockWidth, fillBrush));
        return grid;
    }

    /// <summary>按行排版结果构建 TextBlock（字号/颜色/对齐/行高与两方案共用）。</summary>
    private static TextBlock CreateTextBlock(BannerLineMetrics line, double blockWidth, Brush foreground) => new()
    {
        Text = line.Text,
        FontFamily = FontAssets.BannerFont,
        FontSize = line.FontSize,
        FontWeight = FontWeights.Bold,
        Foreground = foreground,
        Width = blockWidth,
        TextAlignment = ToWpfAlignment(line.Align),
        HorizontalAlignment = HorizontalAlignment.Center,
        LineHeight = line.Height,
        // 允许字体度量略超行高时截断兜底（中英混排行高按字号推定）
        ClipToBounds = false,
    };

    /// <summary>方案 B 描边方向（度）：8 向偏移覆盖四周，形成实心描边环。</summary>
    private static readonly double[] StrokeDirections = { 0, 45, 90, 135, 180, 225, 270, 315 };

    private static System.Windows.TextAlignment ToWpfAlignment(Object1688.Shared.Text.TextAlignment align)
        => align switch
        {
            Object1688.Shared.Text.TextAlignment.Left => System.Windows.TextAlignment.Left,
            Object1688.Shared.Text.TextAlignment.Right => System.Windows.TextAlignment.Right,
            _ => System.Windows.TextAlignment.Center,
        };
}