using System.Runtime.InteropServices;
using System.Windows;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 目标屏工作区（DIP）：坐标基于目标显示器虚拟工作区（不含任务栏/停靠栏，架构 §8.3/AC-48）。
/// </summary>
public readonly record struct ScreenWorkArea(double Left, double Top, double Width, double Height, double DpiScale)
{
    /// <summary>坐标系完整性兜底：主屏回退时为系统工作区。</summary>
    public static ScreenWorkArea Primary { get; } = FromSystemWorkArea();

    private static ScreenWorkArea FromSystemWorkArea()
    {
        var wa = SystemParameters.WorkArea;
        return new ScreenWorkArea(wa.Left, wa.Top, wa.Width, wa.Height, 1.0);
    }
}

/// <summary>
/// 目标显示器解析（架构 §8.3 多显示器/P-MV2 DPI，AC-42/AC-48）。
/// "primary" → 主屏工作区（SystemParameters.WorkArea，DIP）；
/// 显示器索引（1 起）→ 以 Win32 EnumDisplayMonitors 枚举，按该屏 DPI 将物理工作区换算为 DIP。
/// </summary>
public static class ScreenAreaResolver
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITORINFOF_PRIMARY = 0x00000001;
    private const int MDT_EFFECTIVE_DPI = 0;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>单屏信息：物理工作区 + 该屏 DPI 缩放 + 是否主屏。</summary>
    private sealed record MonitorEntry(RECT Work, double Scale, bool IsPrimary);

    /// <summary>解析结果：目标屏工作区（DIP）+ 是否发生回退。</summary>
    public readonly record struct ResolveResult(ScreenWorkArea Area, bool FellBack);

    /// <summary>
    /// 解析目标屏工作区（DIP）。
    /// </summary>
    /// <remarks>
    /// <see cref="ResolveResult.FellBack"/> 为 true 表示目标屏不可用已回退主屏（对应 OVL-W-3004 类告警，调用方记日志）。
    /// </remarks>
    public static ResolveResult Resolve(string? target)
    {
        var entries = EnumerateMonitors();

        if (string.IsNullOrWhiteSpace(target) ||
            string.Equals(target.Trim(), "primary", StringComparison.OrdinalIgnoreCase))
        {
            return new ResolveResult(ResolvePrimary(entries), false);
        }

        if (int.TryParse(target.Trim(), out var index) && index >= 1 && index <= entries.Count)
        {
            return new ResolveResult(FromEntry(entries[index - 1]), false);
        }

        return new ResolveResult(ResolvePrimary(entries), true);
    }

    /// <summary>主屏工作区：优先取 MONITORINFO 主屏标记（混合缩放时精确）；无枚举结果时回退系统参数。</summary>
    private static ScreenWorkArea ResolvePrimary(IReadOnlyList<MonitorEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.IsPrimary)
            {
                return FromEntry(entry);
            }
        }

        // 兜底：无主屏标记时取枚举首位（一般即主屏）
        return entries.Count > 0 ? FromEntry(entries[0]) : ScreenWorkArea.Primary;
    }

    /// <summary>物理工作区（px）按该屏 DPI 缩放换算为 DIP 工作区。</summary>
    private static ScreenWorkArea FromEntry(MonitorEntry entry)
        => new(
            entry.Work.Left / entry.Scale,
            entry.Work.Top / entry.Scale,
            (entry.Work.Right - entry.Work.Left) / entry.Scale,
            (entry.Work.Bottom - entry.Work.Top) / entry.Scale,
            entry.Scale);

    /// <summary>枚举全部显示器工作区（物理像素），并取每屏有效 DPI 缩放（失败按 1.0）。</summary>
    private static List<MonitorEntry> EnumerateMonitors()
    {
        var result = new List<MonitorEntry>();

        MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                var (scaleX, _) = TryGetDpiScale(hMonitor);
                result.Add(new MonitorEntry(info.rcWork, Math.Max(1.0, scaleX), (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }

            return true;
        };

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        return result;
    }

    /// <summary>获取显示器有效 DPI 缩放因子；shcore 不可用/失败时按 1.0 回退（旧系统兼容）。</summary>
    private static (double ScaleX, double ScaleY) TryGetDpiScale(IntPtr hMonitor)
    {
        try
        {
            if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) == 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch (EntryPointNotFoundException)
        {
            // 低于 Win10 的系统无 shcore.dll；按 96 DPI 处理
        }

        return (1.0, 1.0);
    }
}