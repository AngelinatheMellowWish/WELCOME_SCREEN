using System.Globalization;
using System.Runtime.InteropServices;

namespace Object1688.Shared.Crash;

/// <summary>
/// 崩溃转储生成器（架构 §7.2 / AC-13）：调用 DbgHelp <c>MiniDumpWriteDump</c>
/// 将当前进程状态写入 <c>{directory}/crash_{processTag}_{yyyyMMdd_HHmmss}.dmp</c>。
/// 成功/失败均由调用方按错误码语义记录（CRS-I-4004 / CRS-E-4003 / GEN-E-9002）。
/// </summary>
public static class CrashDumper
{
    /// <summary>
    /// 生成当前进程的 MiniDump。
    /// </summary>
    /// <param name="directory">崩溃转储目录（不存在则创建）。</param>
    /// <param name="processTag">进程标签（如 "main"、"overlay"），用于文件名区分。</param>
    /// <returns>转储文件完整路径；失败返回 null（调用方记录 CRS-E-4003）。</returns>
    public static string? TryWriteDump(string directory, string processTag)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var path = Path.Combine(directory, $"crash_{processTag}_{stamp}.dmp");

            using var stream = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            if (!MiniDumpWriteDump(
                GetCurrentProcess(),
                GetCurrentProcessId(),
                stream.SafeFileHandle,
                MiniDumpType.WithFullMemory,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero))
            {
                return null;
            }

            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null; // 目录不可写/句柄失败等：转储失败（CRS-E-4003 语义由调用方承载）
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("dbghelp.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
        MiniDumpType dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    [Flags]
    private enum MiniDumpType
    {
        Normal = 0x00000000,
        WithFullMemory = 0x00000002,
    }
}