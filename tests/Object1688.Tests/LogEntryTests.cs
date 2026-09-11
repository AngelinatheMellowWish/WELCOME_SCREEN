using System.Text.RegularExpressions;
using Object1688.Shared.Logging;

namespace Object1688.Tests;

/// <summary>
/// LogEntry 行格式 / LogLevel 等级映射测试（开发规范 §6.2）。
/// 格式：[yyyy-MM-dd HH:mm:ss.fff] [LEVEL] [module] [errorCode] 消息（错误码可空省略）。
/// </summary>
public class LogEntryTests
{
    private static readonly DateTimeOffset FixedLocal = new(2026, 9, 9, 15, 4, 5, 678, DateTimeOffset.Now.Offset);

    private static readonly Regex LineFormat = new(
        @"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\] \[(DEBUG|INFO|WARN|ERROR|FATAL)\] \[\w+\]( \[[A-Z]{3}-[EIWVF]-\d{4}\])? .+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static LogEntry Entry(
        LogLevel level,
        string module = "main",
        string? errorCode = null,
        string message = "测试消息")
        => new()
        {
            Timestamp = FixedLocal,
            Level = level,
            Module = module,
            ErrorCode = errorCode,
            Message = message,
        };

    [Theory]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Info, "INFO")]
    [InlineData(LogLevel.Warn, "WARN")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Fatal, "FATAL")]
    public void FormatLine_LevelMapping(LogLevel level, string expectedText)
    {
        var line = Entry(level).FormatLine();
        Assert.Contains($"[{expectedText}]", line);
    }

    [Fact]
    public void FormatLine_UnknownLevel_FallsBackToInfo()
    {
        var line = Entry((LogLevel)99).FormatLine();
        Assert.Contains("[INFO]", line);
    }

    [Fact]
    public void FormatLine_IncludesErrorCode_WhenPresent()
    {
        var line = Entry(LogLevel.Warn, errorCode: "CFG-W-1002").FormatLine();
        Assert.Contains("[CFG-W-1002]", line);
    }

    [Fact]
    public void FormatLine_OmitsErrorCode_WhenNull()
    {
        var line = Entry(LogLevel.Info).FormatLine();
        // 无错误码：module 段后直接跟消息，无 [XXXX-X-XXXX] 段
        Assert.DoesNotContain("] [", line.Substring(line.LastIndexOf(']')));
        Assert.EndsWith("[main] 测试消息", line);
    }

    [Fact]
    public void FormatLine_OmitsErrorCode_WhenEmpty()
    {
        var line = Entry(LogLevel.Info, errorCode: string.Empty).FormatLine();
        Assert.DoesNotContain("] [", line.Substring(line.LastIndexOf(']')));
        Assert.EndsWith("[main] 测试消息", line);
    }

    [Fact]
    public void FormatLine_FullStructure_MatchesDocumentedFormat()
    {
        var line = Entry(LogLevel.Warn, "monitor", "MON-W-5002", "目标窗口未命中").FormatLine();
        Assert.Matches(LineFormat, line);
        Assert.StartsWith("[2026-09-09 ", line);
        Assert.EndsWith("[monitor] [MON-W-5002] 目标窗口未命中", line);
    }

    [Fact]
    public void FormatLine_UsesLocalTime_NotUtc()
    {
        var local = FixedLocal.ToLocalTime();
        var expectedPrefix = $"[{local:yyyy-MM-dd HH:mm:ss.fff}]";
        var line = Entry(LogLevel.Info).FormatLine();
        Assert.StartsWith(expectedPrefix, line);
    }

    [Fact]
    public void FormatLine_MessageAppearsVerbatim()
    {
        const string message = "心跳超时，强制重启（第 3 次）";
        var line = Entry(LogLevel.Warn, errorCode: "IPC-W-7002", message: message).FormatLine();
        Assert.EndsWith(message, line);
    }
}