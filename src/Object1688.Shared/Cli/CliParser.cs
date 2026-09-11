using System.Text;

namespace Object1688.Shared.Cli;

/// <summary>
/// 命令行解析器（架构 §8.7 / AC-37）。
/// 支持参数：--config &lt;path&gt;、--lang &lt;zh-CN|en-US&gt;、--debug、--no-autostart、--version。
/// 非法参数/缺值/非法语言值 → 收集错误（CFG-V-1003），由调用方打印用法后退场。
/// </summary>
public static class CliParser
{
    /// <summary>合法语言取值集合。</summary>
    public static readonly IReadOnlySet<string> ValidLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "zh-CN",
        "en-US",
    };

    /// <summary>
    /// 解析命令行参数。
    /// </summary>
    /// <param name="args">原始参数数组（可空/空 = 全部默认）。</param>
    /// <returns>解析结果（含错误列表）。</returns>
    public static CliOptions Parse(string[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return new CliOptions();
        }

        string? configPath = null;
        string? language = null;
        var debug = false;
        var noAutostart = false;
        var showVersion = false;
        var quitRequested = false;
        string? controlCommand = null;
        string? controlText = null;
        string? controlScreen = null;
        var errors = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--config":
                    if (!TryTakeValue(args, ref i, out var config))
                    {
                        errors.Add("--config 缺少路径值");
                    }
                    else
                    {
                        configPath = config;
                    }

                    break;

                case "--lang":
                    if (!TryTakeValue(args, ref i, out var lang))
                    {
                        errors.Add("--lang 缺少语言值");
                    }
                    else if (!ValidLanguages.Contains(lang))
                    {
                        errors.Add($"不支持的 --lang 值 '{lang}'（可选 zh-CN / en-US）");
                    }
                    else
                    {
                        language = lang;
                    }

                    break;

                case "--debug":
                    debug = true;
                    break;

                case "--no-autostart":
                    noAutostart = true;
                    break;

                case "--version":
                    showVersion = true;
                    break;

                case "--quit":
                    quitRequested = true;
                    break;

                case "--control":
                    if (!TryTakeValue(args, ref i, out var control))
                    {
                        errors.Add("--control 缺少命令值");
                    }
                    else
                    {
                        controlCommand = control;
                    }

                    break;

                case "--text":
                    if (!TryTakeValue(args, ref i, out var text))
                    {
                        errors.Add("--text 缺少文字值");
                    }
                    else
                    {
                        controlText = text;
                    }

                    break;

                case "--screen":
                    if (!TryTakeValue(args, ref i, out var screen))
                    {
                        errors.Add("--screen 缺少取值");
                    }
                    else
                    {
                        controlScreen = screen;
                    }

                    break;

                default:
                    errors.Add($"未知参数 '{arg}'");
                    break;
            }
        }

        return new CliOptions
        {
            ConfigPath = configPath,
            Language = language,
            Debug = debug,
            NoAutostart = noAutostart,
            ShowVersion = showVersion,
            QuitRequested = quitRequested,
            ControlCommand = controlCommand,
            ControlText = controlText,
            ControlScreen = controlScreen,
            Errors = errors,
        };
    }

    /// <summary>
    /// 生成用法文本（错误或 --help 时打印）。
    /// </summary>
    public static string BuildUsageText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Object1688 - 进程/窗口监测大字提示工具");
        sb.AppendLine("用法: Object1688.Main.exe [选项]");
        sb.AppendLine("选项:");
        sb.AppendLine("  --config <path>      指定配置文件路径");
        sb.AppendLine("  --lang <zh-CN|en-US> 界面语言");
        sb.AppendLine("  --debug              调试模式（Debug 级日志）");
        sb.AppendLine("  --no-autostart       本轮运行不写入开机自启");
        sb.AppendLine("  --version            打印版本号并退出");
        sb.AppendLine("  --quit               请求既有主进程优雅退出（二次实例转发）");
        sb.AppendLine("  --control <command>  调用运行中的主实例（F-76/F-77）：ping/status/banner/manual/pause/resume/");
        sb.AppendLine("                       toggle-pause/end/reload/config/perf/about/diagnostics/quit");
        sb.AppendLine("  --text <text>        --control banner 的文字内容（支持 \\n 多行）");
        sb.AppendLine("  --screen <primary|n> --control banner 的目标屏");
        return sb.ToString();
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }
}