namespace Object1688.Shared.Config;

/// <summary>
/// 勿扰/定时静默配置节（架构 §5.7 dnd，F-25）。
/// 对应 config.json dnd 对象。
/// </summary>
public sealed class DndConfig
{
    /// <summary>全局暂停开关（托盘开关）。true 时自动触发大字不进入播放队列（手动/测试为强制路径）。缺省 false。</summary>
    public bool Paused { get; init; }

    /// <summary>定时勿扰是否启用。缺省 false。</summary>
    public bool ScheduleEnabled { get; init; }

    /// <summary>投屏/演示自动静默（F-25 扩展/AC-90）：检测到全屏演示/投屏时自动静默自动触发。缺省 false。</summary>
    public bool PresentationAutoSilence { get; init; }

    /// <summary>勿扰时段列表（F-25：星期×起止时间）。缺省空。</summary>
    public required IReadOnlyList<DndScheduleEntry> Schedule { get; init; }
}

/// <summary>
/// 勿扰时段单条（架构 §5.7 dnd.schedule 元素）。
/// </summary>
public sealed class DndScheduleEntry
{
    /// <summary>星期集合（1=周一 … 7=周日，ISO 8601）。</summary>
    public required int[] Days { get; init; }

    /// <summary>起始时间（HH:mm，24 小时制）。</summary>
    public required string Start { get; init; }

    /// <summary>结束时间（HH:mm，24 小时制）。</summary>
    public required string End { get; init; }
}