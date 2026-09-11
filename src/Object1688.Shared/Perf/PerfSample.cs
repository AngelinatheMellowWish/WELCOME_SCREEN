namespace Object1688.Shared.Perf;

/// <summary>
/// 单个性能采样点（架构 §6.1/§6.3 曲线数据源）。
/// 采集方按固定周期（默认 1s）上报；性能窗口按时间窗（60s/5min）从 RingBuffer 切片绘图。
/// </summary>
public sealed record PerfSample(
    DateTimeOffset Time,
    string ProcessTag,
    double CpuPercent,
    long WorkingSetBytes);

/// <summary>
/// 检测/运行事件流条目（架构 §6.3 事件流环形 500）。
/// 文本含动作摘要（如 "r-002 命中 游戏"）；TraceId 可空用于最近一次大字回看关联。
/// </summary>
public sealed record EventItem(
    DateTimeOffset Time,
    string Text,
    string? ErrorCode = null,
    string? TraceId = null);
