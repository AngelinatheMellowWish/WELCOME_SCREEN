using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace Object1688.Main;

/// <summary>
/// 关于窗口（需求书 AC-25 / F-71）：展示版本号、GPL-3.0 与字体 OFL 致谢、数据隐私声明。
/// </summary>
public partial class AboutWindow : Window
{
    /// <summary>初始化关于窗口并填充版本号。</summary>
    public AboutWindow()
    {
        InitializeComponent();
        Title = Object1688.Shared.I18n.LocalizedStrings.Get("AboutWindowTitle");
        VersionText.Text = $"版本 {GetVersion()}";
        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Object1688.Main;component/icons/app.ico", UriKind.Absolute));
        }
        catch (Exception)
        {
            // 图标加载失败不阻断（沿用默认）
        }
    }

    private void OnOpenProjectClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/AngelinatheMellowWish/WELCOME_SCREEN")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // 浏览器打开失败不阻断（无合适错误码，静默）
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString(3) ?? "0.2.0";
    }
}
