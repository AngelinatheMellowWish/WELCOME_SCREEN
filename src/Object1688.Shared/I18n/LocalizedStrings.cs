using System.Globalization;
using System.Resources;

namespace Object1688.Shared.I18n;

/// <summary>
/// 界面文案本地化解析（开发规范 §2.6 / AC-75）：基于 <c>Strings.zh-Hans.resx</c> / <c>Strings.en.resx</c>
/// 编译出的卫星资源（ResourceManager 标准机制），按界面语言取文案；键缺失时回退中性资源；
/// 均缺失时触发 GEN-W-9003 警告回调并返回占位 <c>[key]</c>，防运行时空白。
/// </summary>
public static class LocalizedStrings
{
    private static readonly ResourceManager Manager = new(
        "Object1688.Shared.Resources.Strings",
        typeof(LocalizedStrings).Assembly);

    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> MissingReported = new();

    /// <summary>键缺失回调（GEN-W-9003；由调用方注入日志，测试可注入收集器）。</summary>
    public static Action<string>? MissingKeyHandler { get; set; }

    /// <summary>
    /// 解析界面文案。取词顺序：精确文化 → 显式跨语言回退（zh ↔ en，AC-75 运行回退语义）。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <param name="culture">界面语言；null = 当前 UI 文化。</param>
    /// <returns>文案；两份资源均无该键时触发 GEN-W-9003 并返回 <c>[key]</c>。</returns>
    public static string Get(string key, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        culture ??= CultureInfo.CurrentUICulture;

        var value = SafeGet(key, culture);
        if (value is not null)
        {
            return value;
        }

        // 跨语言显式回退：zh 系缺 → 试 en；其余 → 试 zh-Hans
        var fallback = IsChinese(culture) ? CultureInfo.GetCultureInfo("en") : CultureInfo.GetCultureInfo("zh-Hans");
        value = SafeGet(key, fallback);
        if (value is not null)
        {
            return value;
        }

        // 双缺：GEN-W-9003 警告（每键仅报一次）+ 占位返回
        lock (Gate)
        {
            if (MissingReported.TryAdd(key, key))
            {
                MissingKeyHandler?.Invoke(key);
            }
        }

        return $"[{key}]";
    }

    private static string? SafeGet(string key, CultureInfo culture)
    {
        try
        {
            return Manager.GetString(key, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null; // 对应文化卫星缺失：视为键缺失，走回退/占位
        }
    }

    private static bool IsChinese(CultureInfo culture)
        => culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            || culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
}