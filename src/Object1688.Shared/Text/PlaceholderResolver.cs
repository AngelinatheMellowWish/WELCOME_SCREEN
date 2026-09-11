using System.Globalization;

namespace Object1688.Shared.Text;

/// <summary>
/// 占位符替换器（架构 §3.2/§5.7：{appName} 与 {time}）。
/// {time} 格式由 timeFormat 控制：auto=随系统语言默认时段格式 | HH:mm | HH:mm:ss | datetime(带日期) | 24h（强制 24 小时制）。
/// 未匹配 appName 或未启用变量时保留原文（架构 §3.2）。
/// </summary>
public static class PlaceholderResolver
{
    /// <summary>AppName 占位符（架构 §3.2）：替换为本次命中进程名/窗口标题。</summary>
    public const string AppNameToken = "{appName}";

    /// <summary>时间占位符（架构 §3.2/§5.7）：触发时刻。</summary>
    public const string TimeToken = "{time}";

    /// <summary>
    /// 替换文本中的 {appName} 与 {time}。
    /// </summary>
    /// <param name="text">原始文本（可为空）。</param>
    /// <param name="appName">{appName} 替换值；Null 或空串时保留 {appName} 原文。</param>
    /// <param name="triggerTime">触发时刻；Null 时用当前时间。仅 {time} 存在时求值。</param>
    /// <param name="timeFormat">timeFormat（默认 auto）。见 <see cref="FormatTime"/>。</param>
    public static string Resolve(string? text, string? appName, DateTimeOffset? triggerTime, string timeFormat = "auto")
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = text;
        if (!string.IsNullOrEmpty(appName))
        {
            result = result.Replace(AppNameToken, appName, StringComparison.Ordinal);
        }

        if (result.Contains(TimeToken, StringComparison.Ordinal))
        {
            result = result.Replace(TimeToken, FormatTime(triggerTime ?? DateTimeOffset.Now, timeFormat), StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// 按 timeFormat 格式化触发时刻。
    /// 非法 timeFormat 抛 FormatException 由调用方拦截（对应 CFG-V-1009 级别的校验语义）。
    /// </summary>
    public static string FormatTime(DateTimeOffset time, string timeFormat)
    {
        switch (timeFormat.Trim().ToLowerInvariant())
        {
            case "":
            case "auto":
                // 随语言取默认时段格式：按 ShortTimePattern 判定 12/24 小时制
                // （区分大小写：'t'(AM/PM)/小写'h'(12小时) → 12 小时制；大写'H' → 24 小时制）
                var culture = CultureInfo.CurrentCulture;
                var pattern = culture.DateTimeFormat.ShortTimePattern;
                var is12 = pattern.Contains('t')
                           || pattern.Contains('h');
                return time.ToLocalTime().ToString(is12 ? "h:mm tt" : "HH:mm", culture);

            case "hh:mm":
                return time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

            case "hh:mm:ss":
                return time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            case "datetime":
                return time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            case "24h":
                return time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

            default:
                throw new FormatException($"未知的 timeFormat: {timeFormat} (CFG-V-1009)");
        }
    }
}