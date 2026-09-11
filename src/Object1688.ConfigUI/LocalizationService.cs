using System.Collections;
using System.Globalization;
using System.Resources;
using System.Windows;

namespace Object1688.ConfigUI;

/// <summary>
/// WPF 资源字典本地化（架构 §5.4 / AC-09）。
/// 将 resx 文案填充进一个 ResourceDictionary 并合并到 Application.Resources；
/// XAML 以 <c>{DynamicResource Key}</c> 引用，切换语言时重填该字典即即时刷新（无需重建窗口）。
/// </summary>
internal static class LocalizationService
{
    private static readonly ResourceDictionary Strings = new();
    private static bool _merged;

    /// <summary>初始化（首次合并字典）并按文化填充文案。</summary>
    public static void Initialize(CultureInfo culture)
    {
        if (Application.Current is null)
        {
            return;
        }

        if (!_merged)
        {
            Application.Current.Resources.MergedDictionaries.Add(Strings);
            _merged = true;
        }

        Apply(culture);
    }

    /// <summary>按文化重填资源字典（切换语言时调用）。</summary>
    public static void Apply(CultureInfo culture)
    {
        Strings.Clear();
        var manager = new ResourceManager("Object1688.Shared.Resources.Strings", typeof(Object1688.Shared.I18n.LocalizedStrings).Assembly);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: true);
        if (set is null)
        {
            return;
        }

        foreach (DictionaryEntry entry in set)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                Strings[key] = value;
            }
        }
    }
}
