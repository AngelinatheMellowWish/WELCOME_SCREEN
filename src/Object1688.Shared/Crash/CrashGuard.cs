using System.Globalization;
using System.Text;

namespace Object1688.Shared.Crash;

/// <summary>
/// 全局崩溃护栏（架构 §7.2 / AC-13 / AC-52）：进程启动时安装未处理异常处理器，
/// 在崩溃瞬间生成 MiniDump 到 <c>log/crash/crash_{tag}_{ts}.dmp</c>，并把异常摘要
/// （含 GEN-E-9002 与堆栈）写入伴生 <c>.err.txt</c>。
/// 崩溃处理器不依赖 Logging 进程存活（崩溃方可能正是 Logging 自身），故现场直接落盘 crash 目录。
/// WPF UI 线程未处理异常（DispatcherUnhandledException 未置 Handled）最终亦落入 AppDomain，
/// 故仅挂 AppDomain 即可覆盖各进程（含 Console 型 Logging）。
/// </summary>
public static class CrashGuard
{
    /// <summary>崩溃目录名（相对可执行目录）。</summary>
    public const string CrashDirectoryName = "log/crash";

    /// <summary>崩溃处理器是否已安装（避免二次进程内重复安装）。</summary>
    private static int _installed;

    /// <summary>进程标签（文件名区分）。</summary>
    private static string _processTag = "unknown";

    /// <summary>
    /// 安装未处理异常护栏。各进程入口应在最早阶段调用一次。
    /// </summary>
    /// <param name="processTag">进程标签（如 "main"、"overlay"、"monitor"、"configui"、"logging"）。</param>
    public static void Install(string processTag)
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
        {
            return;
        }

        _processTag = processTag;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Handle(e.ExceptionObject as Exception, isTerminating: true);
    }

    /// <summary>
    /// 执行崩溃落盘：生成 MiniDump + 伴生异常摘要（默认写可执行目录 log/crash）。
    /// </summary>
    /// <param name="ex">未处理异常。</param>
    /// <param name="isTerminating">是否为进程终止型崩溃。</param>
    public static void Handle(Exception? ex, bool isTerminating)
        => Handle(ex, isTerminating, Path.Combine(AppContext.BaseDirectory, CrashDirectoryName));

    /// <summary>
    /// 执行崩溃落盘（可指定崩溃目录，供测试注入验证产物）。
    /// </summary>
    /// <param name="ex">未处理异常。</param>
    /// <param name="isTerminating">是否为进程终止型崩溃。</param>
    /// <param name="crashRoot">崩溃现场目录（已含 crash 段，形如 log/crash）。</param>
    public static void Handle(Exception? ex, bool isTerminating, string crashRoot)
    {
        var path = CrashDumper.TryWriteDump(crashRoot, _processTag);

        try
        {
            Directory.CreateDirectory(crashRoot);
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var errPath = Path.Combine(crashRoot, $"crash_{_processTag}_{stamp}.err.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] [FATAL] [{_processTag}] [{ErrorCodes.GenericCrash}] 程序崩溃");
            sb.AppendLine("转储文件: " + (path ?? "（生成失败，CRS-E-4003）"));
            if (ex is not null)
            {
                sb.AppendLine("异常类型: " + ex.GetType().FullName);
                sb.AppendLine("异常消息: " + ex.Message);
                sb.AppendLine("堆栈:");
                sb.AppendLine(ex.StackTrace ?? "(null)");
            }

            File.WriteAllText(errPath, sb.ToString(), Encoding.UTF8);
        }
        catch (IOException)
        {
            // 现场落盘失败：交由 OS 层日志/退出兜底，无法再上报
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }
    }
}