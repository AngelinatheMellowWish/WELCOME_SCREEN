using System.IO;
using System.Runtime.InteropServices;

namespace Object1688.Main;

/// <summary>
/// 控制台附着工具。
/// Main 为 WinExe（WPF）无自带控制台；从命令行被拉起时附着父进程控制台，
/// 使 --version / 用法提示等 AC-78 要求的"独立打印退出"真实可见。
/// </summary>
internal static class NativeConsole
{
    private const uint AttachParentProcess = uint.MaxValue; // -1

    private static bool _attached;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    /// <summary>
    /// 尝试附着父控制台并向其输出一行文本。
    /// 无控制台环境（如资源管理器双击）下静默丢弃输出，不影响退出码；
    /// 标准输出被重定向（脚本调用）时即便附着失败也照常写入，保证脚本可捕获结果。
    /// </summary>
    /// <param name="text">要输出的文本。</param>
    public static void Print(string text)
    {
        EnsureAttached();
        try
        {
            Console.WriteLine(text);
        }
        catch (IOException)
        {
            // 父控制台已关闭 / 无标准输出，忽略
        }
        catch (ObjectDisposedException)
        {
            // 标准输出已释放，忽略
        }
    }

    private static void EnsureAttached()
    {
        if (_attached)
        {
            return;
        }

        _attached = AttachConsole(AttachParentProcess);
        if (_attached)
        {
            try
            {
                var stdout = Console.OpenStandardOutput();
                Console.SetOut(new StreamWriter(stdout) { AutoFlush = true });
            }
            catch (IOException)
            {
                // 无法重定向标准输出，降级为直接 WriteLine
            }
        }
    }
}