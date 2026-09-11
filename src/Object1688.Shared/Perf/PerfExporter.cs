using System.Globalization;
using System.Text;
using Object1688.Shared.Stats;

namespace Object1688.Shared.Perf;

/// <summary>
/// 性能/事件数据导出器（架构 §6.3 / AC-39/AC-91，F-30 扩展）。
/// 输出至 <c>log/exports/</c>；CSV 采用 **UTF-8 BOM**（兼容 Excel 中文直开）；
/// 文件名自动追加 <c>yyyyMMdd_HHmmss</c> 时间戳防覆盖（同秒自动加序号）。
/// 错误码：写失败抛 IOException（消息含 IO-E-6001）。
/// </summary>
public static class PerfExporter
{
    /// <summary>导出默认根目录（相对程序基目录 log/exports/）。</summary>
    public static string DefaultExportDirectory => Path.Combine(AppContext.BaseDirectory, "log", "exports");

    /// <summary>性能采样 CSV 列头。</summary>
    private const string CsvHeader = "timestamp,processTag,cpuPercent,workingSetBytes";

    /// <summary>
    /// 导出性能采样为 CSV（UTF-8 BOM）。
    /// </summary>
    /// <param name="samples">采样点（已按时间序）。</param>
    /// <param name="targetDirectory">目标目录；null = <see cref="DefaultExportDirectory"/>。</param>
    /// <returns>写出文件完整路径。</returns>
    /// <exception cref="IOException">写盘失败（消息含 IO-E-6001）。</exception>
    public static string ExportPerfCsv(IReadOnlyList<PerfSample> samples, string? targetDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var path = ReservePath(targetDirectory, "perf", "csv");
        var sb = new StringBuilder();
        sb.AppendLine(CsvHeader);
        foreach (var s in samples)
        {
            sb.Append(s.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(s.ProcessTag).Append(',');
            sb.Append(s.CpuPercent.ToString("0.0", CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(s.WorkingSetBytes.ToString(CultureInfo.InvariantCulture));
        }

        WriteAllTextUtf8Bom(path, sb.ToString());
        return path;
    }

    /// <summary>
    /// 导出触发统计为 JSON（对齐 stats.json 结构）。
    /// </summary>
    /// <param name="data">统计快照。</param>
    /// <param name="targetDirectory">目标目录；null = <see cref="DefaultExportDirectory"/>。</param>
    /// <returns>写出文件完整路径。</returns>
    /// <exception cref="IOException">写盘失败（消息含 IO-E-6001）。</exception>
    public static string ExportStatsJson(StatsData data, string? targetDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var path = ReservePath(targetDirectory, "stats", "json");
        // 复用磁盘 JSON 序列化选项（camelCase/缩进/UTF-8 原样）
        var json = System.Text.Json.JsonSerializer.Serialize(data, Object1688.Shared.Config.ConfigSerializer.Options);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    /// <summary>
    /// 导出事件流为 CSV（UTF-8 BOM）。
    /// </summary>
    /// <param name="events">事件条目（已按时间序）。</param>
    /// <param name="targetDirectory">目标目录；null = <see cref="DefaultExportDirectory"/>。</param>
    /// <returns>写出文件完整路径。</returns>
    /// <exception cref="IOException">写盘失败（消息含 IO-E-6001）。</exception>
    public static string ExportEventsCsv(IReadOnlyList<EventItem> events, string? targetDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        var path = ReservePath(targetDirectory, "events", "csv");
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,text,errorCode,traceId");
        foreach (var e in events)
        {
            sb.Append(e.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(CsvEscape(e.Text)).Append(',');
            sb.Append(CsvEscape(e.ErrorCode ?? string.Empty)).Append(',');
            sb.AppendLine(CsvEscape(e.TraceId ?? string.Empty));
        }

        WriteAllTextUtf8Bom(path, sb.ToString());
        return path;
    }

    /// <summary>
    /// 导出事件流为 TXT（人读：一行一条，`[时间] 文本`）。
    /// </summary>
    /// <param name="events">事件条目（已按时间序）。</param>
    /// <param name="targetDirectory">目标目录；null = <see cref="DefaultExportDirectory"/>。</param>
    /// <returns>写出文件完整路径。</returns>
    /// <exception cref="IOException">写盘失败（消息含 IO-E-6001）。</exception>
    public static string ExportEventsTxt(IReadOnlyList<EventItem> events, string? targetDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        var path = ReservePath(targetDirectory, "events", "txt");
        var sb = new StringBuilder();
        foreach (var e in events)
        {
            var ts = e.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var code = string.IsNullOrEmpty(e.ErrorCode) ? string.Empty : $" [{e.ErrorCode}]";
            sb.Append('[').Append(ts).Append(']').Append(code).Append(' ').AppendLine(e.Text);
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static string ReservePath(string? targetDirectory, string prefix, string ext)
    {
        var dir = targetDirectory ?? DefaultExportDirectory;
        Directory.CreateDirectory(dir);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(dir, $"{prefix}-{stamp}.{ext}");
        var seq = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(dir, $"{prefix}-{stamp}_{seq++}.{ext}");
        }

        return path;
    }

    private static void WriteAllTextUtf8Bom(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"导出写盘失败（{ErrorCodes.IoExportWriteFailed}）：{path}：{ex.Message}");
        }
    }

    private static string CsvEscape(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
