namespace Object1688.Shared.Config;

/// <summary>
/// 应用完整配置模型（架构 §5.7 全局配置 Schema，config.json 根对象）。
/// 与 config/config.default.json（模板）保持同一 schema，模板为唯一真源；字段缺省以模板为准。
/// </summary>
public sealed class AppConfig
{
    /// <summary>schema 版本（兼容治理标识，开发规范 §9.2）。当前 1。</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>界面语言（zh-CN / en-US）。null = 首次启动跟随系统显示语言（F-60）。</summary>
    public string? Language { get; init; }

    /// <summary>全局默认节。</summary>
    public required GlobalConfig Global { get; init; }

    /// <summary>欢迎大字节（F-11）。</summary>
    public required WelcomeConfig Welcome { get; init; }

    /// <summary>手动大字节（F-12）。</summary>
    public required ManualConfig Manual { get; init; }

    /// <summary>触发规则列表（架构 §4.2；顺序即同命中优先级）。</summary>
    public required IReadOnlyList<RuleConfig> Rules { get; init; }

    /// <summary>勿扰/定时静默节（F-25）。</summary>
    public required DndConfig Dnd { get; init; }

    /// <summary>提示音节（F-08）。</summary>
    public required SoundConfig Sound { get; init; }

    /// <summary>开机自启（F-24，写/删注册表 HKCU Run 项）。缺省 true（默认开启）。</summary>
    public bool Autostart { get; init; } = true;

    /// <summary>首次运行引导标志（F-13）。缺省 true。</summary>
    public bool FirstRun { get; init; } = true;

    /// <summary>窗口布局记忆（AC-61/F-30，架构 §5.7 uiState 节）。可空：缺省时窗口用默认布局。</summary>
    public UiStateConfig? UiState { get; init; }
}