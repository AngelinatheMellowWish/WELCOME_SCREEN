using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 大字叠加窗口（架构 §3.1 窗口特性，AC-02/AC-03）。
/// 透明置顶 + 通透（WS_EX_TRANSPARENT 鼠标点击穿透）+ 不抢焦点（ShowActivated=False + WS_EX_NOACTIVATE）。
/// 窗口覆盖目标屏工作区，内容按 position 在内部对齐（AC-48）。
/// </summary>
public sealed class BannerWindow : Window
{
    private const int GWL_EXSTYLE = -20;

    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>创建覆盖给定工作区的叠加窗口。</summary>
    /// <param name="workArea">目标屏工作区（DIP）。窗口覆盖该区域，内容在内部按 position 对齐。</param>
    /// <param name="content">窗口内容（BannerVisualFactory 构建的整块大字）。</param>
    public BannerWindow(ScreenWorkArea workArea, UIElement content)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;

        Left = workArea.Left;
        Top = workArea.Top;
        Width = workArea.Width;
        Height = workArea.Height;

        Content = content;
    }

    /// <summary>窗口句柄就绪后追加通透/免激活扩展样式（AC-02 输入穿透 + 不抢焦点）。</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        _ = SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }
}