using System.IO.Compression;
using Object1688.Shared.Diagnostics;

namespace Object1688.Tests;

/// <summary>
/// 一键诊断包测试（F-30 扩展/AC-83）。
/// 契约点：打包环境信息/配置/统计/日志；缺失文件不阻断但标记 Partial（IO-W-6004 语义）。
/// </summary>
public class DiagnosticPackagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "o1688-diag-" + Guid.NewGuid().ToString("N"));

    public DiagnosticPackagerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // 忽略清理失败
        }
    }

    [Fact]
    public void Create_IncludesEnvironmentConfigStatsAndLogs()
    {
        var logDir = Path.Combine(_dir, "log");
        Directory.CreateDirectory(logDir);
        File.WriteAllText(Path.Combine(logDir, "app.log"), "line");
        var config = Path.Combine(_dir, "config.json");
        File.WriteAllText(config, "{}");
        var stats = Path.Combine(_dir, "stats.json");
        File.WriteAllText(stats, "{}");

        var result = DiagnosticPackager.Create(Path.Combine(_dir, "out"), logDir, config, stats, "0.1.0");

        Assert.True(File.Exists(result.Path));
        Assert.False(result.Partial);
        using var zip = ZipFile.OpenRead(result.Path);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("environment.txt", names);
        Assert.Contains("config.json", names);
        Assert.Contains("stats.json", names);
        Assert.Contains("logs/app.log", names);
    }

    [Fact]
    public void Create_MissingFiles_StillCreatesPackAndMarksPartial()
    {
        var result = DiagnosticPackager.Create(
            Path.Combine(_dir, "out2"),
            Path.Combine(_dir, "no-such-log-dir"),
            Path.Combine(_dir, "missing.json"),
            Path.Combine(_dir, "missing2.json"),
            "0.1.0");

        Assert.True(File.Exists(result.Path));
        Assert.True(result.Partial);
        Assert.NotEmpty(result.Warnings);
    }
}
