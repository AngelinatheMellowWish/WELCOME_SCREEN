using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Object1688.Shared.Monitor;

namespace Object1688.Overlay.Monitoring;

/// <summary>
/// Win32 监测快照提供者（架构 §4.1，M3c）。
/// 职责：进程快照（Process.GetProcesses，进程名含 ".exe" 对齐模板约定）
/// + 顶层窗口枚举（EnumWindows：仅可见/无属主/非空标题）
/// + 全屏检测（窗口矩形覆盖所在屏完整显示区，±4px 容差，AC-05）。
/// 访问受限进程（系统进程等）逐条跳过并置 ExecutablePath 为 null，不阻塞整轮快照。
/// 线程模型：由监测循环单线程周期调用（MonitorLoop.PollOnce），无内部锁。
/// </summary>
public sealed class WindowsMonitorProvider : IMonitorSnapshotProvider
{
    private const uint GW_OWNER = 4;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int FullscreenTolerancePx = 4;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <inheritdoc />
    public MonitorSnapshot TakeSnapshot()
    {
        return new MonitorSnapshot(
            EnumerateProcesses(),
            EnumerateWindows());
    }

    /// <summary>进程快照：逐条容错，单进程失败（退出/权限）跳过不阻塞整轮。</summary>
    private static List<MonitorProcessInfo> EnumerateProcesses()
    {
        var processes = new List<MonitorProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = ReadProcessName(process);
                var executablePath = ReadExecutablePath(process);
                processes.Add(new MonitorProcessInfo(process.Id, name, executablePath));
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                // 进程已退出/权限受限：跳过单个进程（循环级 MON-E-5001 仅覆盖整轮失败）
            }
            finally
            {
                process.Dispose();
            }
        }

        return processes;
    }

    /// <summary>
    /// 进程名（含 ".exe"，如 "chrome.exe"）：优先取主模块名（天然含扩展名）；
    /// MainModule 权限受限时回退 ProcessName 并补 ".exe" 扩展名。
    /// </summary>
    private static string ReadProcessName(Process process)
    {
        try
        {
            var moduleName = process.MainModule?.ModuleName;
            if (!string.IsNullOrWhiteSpace(moduleName))
            {
                return moduleName;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // 权限不足 → 回退 ProcessName 补扩展名
        }

        var processName = process.ProcessName;
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : processName + ".exe";
    }

    /// <summary>可执行文件路径（AC-45 同目录自身判定用）；权限受限时置 null。</summary>
    private static string? ReadExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>顶层窗口枚举：仅可见、无属主（跳过 owned 子窗口）、非空标题；全屏标志随条目携带。</summary>
    private static List<MonitorWindowInfo> EnumerateWindows()
    {
        var windows = new List<MonitorWindowInfo>(64);

        EnumWindowsProc callback = (hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            // 仅真正的顶层窗口：跳过被属主窗口拥有的子窗口（对话框/工具窗等）
            if (GetWindow(hWnd, GW_OWNER) != IntPtr.Zero)
            {
                return true;
            }

            var length = GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return true; // 无标题窗口不参与标题匹配
            }

            var title = new StringBuilder(length + 1);
            _ = GetWindowText(hWnd, title, title.Capacity);
            if (title.Length == 0)
            {
                return true;
            }

            _ = GetWindowThreadProcessId(hWnd, out var ownerPid);
            windows.Add(new MonitorWindowInfo((int)ownerPid, title.ToString(), IsFullscreen(hWnd)));
            return true;
        };

        _ = EnumWindows(callback, IntPtr.Zero);
        return windows;
    }

    /// <summary>
    /// 全屏判定（AC-05）：窗口矩形覆盖所在屏完整显示区 rcMonitor（±4px 容差）。
    /// 以 rcMonitor 而非 rcWork 为基准：最大化窗口不遮任务栏（未覆盖 rcMonitor），
    /// 真正全屏（游戏/播放器，任务栏隐藏）才判定为全屏。
    /// </summary>
    private static bool IsFullscreen(IntPtr hWnd)
    {
        if (!GetWindowRect(hWnd, out var rect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        return rect.Left <= info.rcMonitor.Left + FullscreenTolerancePx
            && rect.Top <= info.rcMonitor.Top + FullscreenTolerancePx
            && rect.Right >= info.rcMonitor.Right - FullscreenTolerancePx
            && rect.Bottom >= info.rcMonitor.Bottom - FullscreenTolerancePx;
    }
}