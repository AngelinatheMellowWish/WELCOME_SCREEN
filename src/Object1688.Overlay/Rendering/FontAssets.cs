using System.Globalization;
using System.Windows.Media;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 大字渲染字体资产（架构 §3.3/§3.4）。
/// 嵌入 Montserrat（OFL）作为西文字形，中文经 FontFamily 级联回退系统中文字体（微软雅黑/等），
/// 不嵌入大体积中文字体（架构 §3.4 决策）。嵌入字体加载失败时使用纯系统回退（OVL-E-3005 由启动自检负责）。
/// </summary>
public static class FontAssets
{
    /// <summary>嵌入字体家族名（csproj 中 Resource 内嵌，pack URI 引用）。</summary>
    private const string EmbeddedFamilyName = "Montserrat";

    /// <summary>
    /// 大字渲染字体族：嵌入 Montserrat + 中文回退链。
    /// WPF FontFamily 支持逗号分隔回退列表，逐字形回退缺失文字（AC-43）。
    /// </summary>
    public static FontFamily BannerFont { get; } = BuildBannerFont();

    private static FontFamily BuildBannerFont()
    {
        var baseUri = new Uri("pack://application:,,,/Object1688.Overlay;component/fonts/");
        var familyName = $"./#{EmbeddedFamilyName}, Microsoft YaHei UI, Microsoft YaHei, Segoe UI, sans-serif";
        return new FontFamily(baseUri, familyName);
    }

    /// <summary>
    /// 将 #RRGGBB / #AARRGGBB 颜色文本解析为 SolidColorBrush。
    /// 非法输入返回白色（渲染容错，不抛异常）。
    /// </summary>
    public static SolidColorBrush ParseColor(string? hex, string fallbackHex = "#FFFFFF")
    {
        var source = string.IsNullOrWhiteSpace(hex) ? fallbackHex : hex!.Trim();

        if (source.StartsWith('#'))
        {
            var body = source[1..];
            if (body.Length == 6 && byte.TryParse(body.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                && byte.TryParse(body.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                && byte.TryParse(body.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return new SolidColorBrush(Color.FromRgb(r, g, b));
            }

            if (body.Length == 8 && byte.TryParse(body.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rr)
                && byte.TryParse(body.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gg)
                && byte.TryParse(body.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bb)
                && byte.TryParse(body.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var aa))
            {
                return new SolidColorBrush(Color.FromArgb(aa, rr, gg, bb));
            }
        }

        return new SolidColorBrush(Colors.White);
    }
}