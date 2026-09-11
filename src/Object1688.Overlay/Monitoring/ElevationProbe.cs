using System.Runtime.InteropServices;

namespace Object1688.Overlay.Monitoring;

/// <summary>
/// 进程完整性级别探测（NFR-04 扩展/AC-94）。
/// 判定目标进程是否为提升完整性级别（管理员/UAC 提升）——Overlay（asInvoker）无法覆盖此类窗口，
/// 命中时应记 OVL-W-3008 并托盘提示，避免用户误以为失效。
/// </summary>
internal static class ElevationProbe
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevationInfo
    {
        public int TokenIsElevated;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

    /// <summary>
    /// 判定指定进程是否为提升完整性（管理员）。无法访问/查询失败一律按 false（不误报）。
    /// </summary>
    public static bool IsProcessElevated(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        IntPtr process = IntPtr.Zero;
        IntPtr token = IntPtr.Zero;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
            {
                return false;
            }

            if (!OpenProcessToken(process, TokenQuery, out token))
            {
                return false;
            }

            var size = Marshal.SizeOf<TokenElevationInfo>();
            buffer = Marshal.AllocHGlobal(size);
            if (!GetTokenInformation(token, TokenElevation, buffer, size, out _))
            {
                return false;
            }

            return Marshal.PtrToStructure<TokenElevationInfo>(buffer).TokenIsElevated != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }

            if (token != IntPtr.Zero)
            {
                _ = CloseHandle(token);
            }

            if (process != IntPtr.Zero)
            {
                _ = CloseHandle(process);
            }
        }
    }
}
