namespace Object1688.Shared.Config;

/// <summary>
/// 全局配置节（架构 §5.7 global）。
/// 对应 config.json global 对象；字段缺省以模板 config.default.json 为准。
/// </summary>
public sealed class GlobalConfig
{
    /// <summary>全局默认字号（DIP）。缺省 96。</summary>
    public double DefaultFontSize { get; init; } = 96;

    /// <summary>全局默认位置（center | top-center | custom {x,y}）。缺省 center。</summary>
    public string DefaultPosition { get; init; } = "center";

    /// <summary>全局默认描边色（#RRGGBB）。缺省黑。</summary>
    public string DefaultOutlineColor { get; init; } = "#000000";

    /// <summary>全局默认描边宽度（DIP）。缺省 0（无描边）。</summary>
    public double DefaultOutlineWidth { get; init; }

    /// <summary>全局默认描边方式（架构 §3.3）：shadow=方案 A 柔光 / stroke=方案 B 精确。缺省 shadow。</summary>
    public string OutlineMode { get; init; } = "shadow";

    /// <summary>全局默认目标屏（NFR-05）。"primary" 或显示器索引号。缺省 "primary"。</summary>
    public string TargetScreen { get; init; } = "primary";

    /// <summary>全局字体覆盖（F-05 扩展）。空串 = 嵌入字体；非空 = 系统已装字体名，加载失败回退嵌入字体（OVL-W-3006）。</summary>
    public string FontFamily { get; init; } = string.Empty;

    /// <summary>{time} 占位符格式（F-04 扩展，CFG-V-1009）。auto=随语言默认 | HH:mm | HH:mm:ss | datetime（带日期）| 24h。缺省 auto。</summary>
    public string TimeFormat { get; init; } = "auto";

    /// <summary>进程/窗口监测轮询间隔（毫秒，架构 §4.1/§4.5）。缺省 1500。</summary>
    public int MonitorPollIntervalMs { get; init; } = 1500;

    /// <summary>同进程 PID 去重窗口（秒，架构 §4.4）。缺省 10。</summary>
    public int DedupeWindowSeconds { get; init; } = 10;

    /// <summary>规则级最小触发间隔兜底默认值（秒，架构 §4.4）。缺省 2。</summary>
    public int MinTriggerIntervalSeconds { get; init; } = 2;

    /// <summary>日志级别（DEBUG/INFO/WARN/ERROR）。缺省 INFO。</summary>
    public string LogLevel { get; init; } = "INFO";

    /// <summary>日志单文件大小上限（MB）。缺省 5。</summary>
    public int LogMaxSizeMB { get; init; } = 5;

    /// <summary>日志保留份数。缺省 10。</summary>
    public int LogRetainCount { get; init; } = 10;

    /// <summary>DPI 感知模式。缺省 per-monitor-v2。</summary>
    public string DpiAwareness { get; init; } = "per-monitor-v2";

    /// <summary>大字触发瞬间渲染开销告警阈值：CPU 时间（毫秒）。超阈值记 OVL-W-3007（NFR-02 扩展/AC-95）。缺省 400。</summary>
    public int RenderCostCpuWarnMs { get; init; } = 400;

    /// <summary>大字触发瞬间渲染开销告警阈值：内存增量（MB）。超阈值记 OVL-W-3007（NFR-02 扩展/AC-95）。缺省 80。</summary>
    public int RenderCostMemWarnMB { get; init; } = 80;
}