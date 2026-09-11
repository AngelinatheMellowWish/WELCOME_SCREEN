namespace Object1688.Shared.Logging;

/// <summary>
/// 日志级别（开发规范 §6.2：DEBUG/INFO/WARN/ERROR/FATAL 五级）。
/// </summary>
public enum LogLevel
{
    /// <summary>调试（仅 --debug 时记录）。</summary>
    Debug,

    /// <summary>信息。</summary>
    Info,

    /// <summary>警告（可恢复）。</summary>
    Warn,

    /// <summary>错误（致命/需关注）。</summary>
    Error,

    /// <summary>致命（程序无法继续，如日志服务自身故障）。</summary>
    Fatal,
}