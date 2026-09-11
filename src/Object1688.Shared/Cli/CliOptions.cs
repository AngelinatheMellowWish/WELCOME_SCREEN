namespace Object1688.Shared.Cli;

/// <summary>
/// 命令行解析结果（架构 §8.7 / AC-37）。
/// </summary>
public sealed class CliOptions
{
    /// <summary>--config &lt;path&gt;：指定配置文件路径（null 表示未指定，使用默认链）。</summary>
    public string? ConfigPath { get; init; }

    /// <summary>--lang &lt;zh-CN|en-US&gt;：界面语言（null 表示使用系统默认）。</summary>
    public string? Language { get; init; }

    /// <summary>--debug：调试模式（日志级别升至 Debug，控制台可见）。</summary>
    public bool Debug { get; init; }

    /// <summary>--no-autostart：本轮运行不写入/不修改开机自启项。</summary>
    public bool NoAutostart { get; init; }

    /// <summary>--version：仅打印版本号并退出（AC-78：独立打印退出）。</summary>
    public bool ShowVersion { get; init; }

    /// <summary>--quit：向既有的主进程请求优雅退出（AC-78 二次实例参数转发）。
    /// 无主进程运行（本实例为首实例）时无副作用，正常启动本轮运行。</summary>
    public bool QuitRequested { get; init; }

    /// <summary>--control &lt;command&gt;：作为控制客户端调用运行中的主实例（F-77/AC-98，独立短生命周期进程）。</summary>
    public string? ControlCommand { get; init; }

    /// <summary>--text &lt;text&gt;：banner 控制命令的文字内容（支持 <c>\n</c> 多行）。</summary>
    public string? ControlText { get; init; }

    /// <summary>--screen &lt;primary|索引&gt;：banner 控制命令的目标屏。</summary>
    public string? ControlScreen { get; init; }

    /// <summary>解析是否成功（失败时 Errors 非空，应打印用法并以 CFG-V-1003 退出）。</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>全部校验错误（对应 CFG-V-1003）。</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];
}