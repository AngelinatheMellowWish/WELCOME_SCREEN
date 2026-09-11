using System.Globalization;

namespace Object1688.Shared.Logging;

/// <summary>
/// 日志条目（经 IPC LogEntry 消息送往 Logging 进程落盘）。
/// 格式（开发规范 §6.2）：[时间戳] [级别] [模块] [错误码] 消息
/// </summary>
public sealed class LogEntry
{
    /// <summary>记录时间（UTC）。</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>日志级别。</summary>
    public required LogLevel Level { get; init; }

    /// <summary>来源模块（如 "main"、"monitor"、"overlay"）。</summary>
    public required string Module { get; init; }

    /// <summary>关联错误码（ErrorCodes 常量，可空）。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>日志消息正文（中文）。</summary>
    public required string Message { get; init; }

    /// <summary>
    /// 标准日志行（[2026-09-08 09:15:30.123] [INFO] [main] [GEN-W-9004] 消息）。
    /// </summary>
    public string FormatLine()
    {
        var level = Level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warn => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Fatal => "FATAL",
            _ => "INFO",
        };

        var timestamp = Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var code = string.IsNullOrEmpty(ErrorCode) ? string.Empty : $" [{ErrorCode}]";
        return $"[{timestamp}] [{level}] [{Module}]{code} {Message}";
    }
}