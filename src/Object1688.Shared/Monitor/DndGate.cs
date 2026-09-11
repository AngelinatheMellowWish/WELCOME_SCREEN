using Object1688.Shared.Config;

namespace Object1688.Shared.Monitor;

/// <summary>
/// 勿扰/暂停常驻裁决器（需求书 F-25/AC-31/AC-90，架构 §4.4 静默裁决）。
/// Main 收到 TriggerEvent 后调用：处于全局暂停或定时勿扰时段内且非强制路径（手动/测试）→ 抑制自动大字，
/// 事件照常记录。跨午夜时段（end ≤ start 视为覆盖次日凌晨）正确判定；星期按 ISO（1=周一…7=周日）。
/// 纯逻辑无 IO，便于单测。
/// </summary>
public static class DndGate
{
    /// <summary>
    /// 判定当前时刻是否应静默（抑制）自动触发大字。
    /// </summary>
    /// <param name="dnd">勿扰配置节。</param>
    /// <param name="nowLocal">当前本地时刻（用于时段/星期判定）。</param>
    /// <param name="isForced">是否为强制路径（手动触发/测试显示）——强制路径不受静默（F-25 扩展）。</param>
    /// <param name="presentationActive">当前是否检测到全屏演示/投屏（AC-90；仅在 <see cref="DndConfig.PresentationAutoSilence"/> 开启时生效）。</param>
    /// <returns>true = 应抑制自动大字。</returns>
    public static bool IsSuppressed(DndConfig dnd, DateTimeOffset nowLocal, bool isForced, bool presentationActive = false)
    {
        ArgumentNullException.ThrowIfNull(dnd);
        if (isForced)
        {
            return false; // 手动/测试强制路径，不受勿扰/暂停
        }

        if (dnd.Paused)
        {
            return true; // 全局暂停：自动触发全部抑制
        }

        if (dnd.PresentationAutoSilence && presentationActive)
        {
            return true; // 投屏/演示自动静默（F-25 扩展/AC-90）
        }

        if (!dnd.ScheduleEnabled || dnd.Schedule is null || dnd.Schedule.Count == 0)
        {
            return false;
        }

        var time = TimeOnly.FromDateTime(nowLocal.LocalDateTime);
        var isoWeekday = IsoDayOfWeek(nowLocal.DayOfWeek);
        foreach (var entry in dnd.Schedule)
        {
            if (!DayMatches(entry.Days, isoWeekday))
            {
                continue;
            }

            if (InTimeWindow(entry.Start, entry.End, time))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ISO 星期（1=周一…7=周日）。</summary>
    public static int IsoDayOfWeek(DayOfWeek day)
        => day == DayOfWeek.Sunday ? 7 : (int)day;

    /// <summary>星期集合是否含目标（空集合视为不匹配）。</summary>
    public static bool DayMatches(IReadOnlyCollection<int> days, int isoWeekday)
        => days is { Count: > 0 } && days.Contains(isoWeekday);

    /// <summary>
    /// 判定当前时间是否落在 [start,end) 时段；end ≤ start 视为跨午夜（[start,24:00) ∪ [00:00,end)）。
    /// 格式非法（无法解析 HH:mm）的时段视为永不匹配（校验由 ConfigValidator 承担 CFG-V-1009）。
    /// </summary>
    public static bool InTimeWindow(string start, string end, TimeOnly time)
    {
        if (!TimeOnly.TryParseExact(start, "HH:mm", out var s)
            || !TimeOnly.TryParseExact(end, "HH:mm", out var e))
        {
            return false;
        }

        if (e > s)
        {
            return time >= s && time < e;
        }

        if (e == s)
        {
            return time == s; // 零长窗口（同刻）：仅当完全相等
        }

        // 跨午夜：晚间到次日凌晨
        return time >= s || time < e;
    }
}
