using Object1688.Shared.Logging;

namespace Object1688.Tests;

/// <summary>
/// LogFileWriter 按天轮转 / 大小轮转 / 保留数量清理测试（架构 §7.1、开发规范 §6.4）。
/// </summary>
public class LogFileWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "Object1688.LogFileWriterTests",
        Guid.NewGuid().ToString("N"));

    private static DateTimeOffset Day(string ymd) => DateTimeOffset.ParseExact(
        ymd, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string ReadAll(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs);
        return sr.ReadToEnd();
    }

    private static string[] ReadAllLines(string path)
        => ReadAll(path).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(l => l.Length > 0).ToArray();

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // 测试清理不阻断结论
        }
    }

    [Fact]
    public void Append_CreatesActiveFile_WithContent()
    {
        using var w = new LogFileWriter(_dir);
        w.Append("第一行", Day("2026-09-09"));

        Assert.True(File.Exists(w.ActivePath));
        Assert.Contains("第一行", ReadAll(w.ActivePath));
    }

    [Fact]
    public void Append_SameDay_NoRotation()
    {
        using var w = new LogFileWriter(_dir);
        w.Append("a", Day("2026-09-09"));
        w.Append("b", Day("2026-09-09"));

        Assert.True(File.Exists(w.ActivePath));
        Assert.Empty(Directory.GetFiles(_dir, "app.*.log"));
        Assert.Equal(new[] { "a", "b" }, ReadAllLines(w.ActivePath));
    }

    [Fact]
    public void Append_NextDay_RotatesWithDateSuffix()
    {
        using var w = new LogFileWriter(_dir);
        w.Append("昨日", Day("2026-09-09"));
        w.Append("今日", Day("2026-09-10"));

        var archived = Path.Combine(_dir, "app.2026-09-09.log");
        Assert.True(File.Exists(archived));
        Assert.Contains("昨日", ReadAll(archived));
        Assert.Contains("今日", ReadAll(w.ActivePath));
    }

    [Fact]
    public void Append_OverSize_RotatesSameDay()
    {
        using var w = new LogFileWriter(_dir, rotationThresholdBytes: 16);
        const string chunk = "0123456789ABCDEF0123456"; // 24 字节 > 16 阈值
        w.Append(chunk, Day("2026-09-09")); // 空文件，不轮转
        w.Append(chunk, Day("2026-09-09")); // 超阈值 → 归档 app.2026-09-09.log
        w.Append(chunk, Day("2026-09-09")); // 同日再超 → 递增 app.2026-09-09.1.log
        w.Append("tail", Day("2026-09-09"));

        var archives = Directory.GetFiles(_dir, "app.*.log");
        Assert.Equal(3, archives.Length); // 09-09.log / .1.log / .2.log
        Assert.Contains(archives, p => p.EndsWith("app.2026-09-09.log"));
        Assert.Contains(archives, p => p.EndsWith("app.2026-09-09.1.log"));
        Assert.Contains(archives, p => p.EndsWith("app.2026-09-09.2.log"));
        Assert.Contains("tail", ReadAll(w.ActivePath));
    }

    [Fact]
    public void Append_BeyondRetainedCount_DeletesOldest()
    {
        using var w = new LogFileWriter(_dir, retainedFileCount: 2);
        w.Append("d1", Day("2026-09-01"));
        w.Append("d2", Day("2026-09-02"));
        w.Append("d3", Day("2026-09-03"));
        w.Append("d4", Day("2026-09-04"));

        // d4 仍在活动文件；归档保留 2 个：仅保留最近两次归档
        // （d1→09-01 归档在 d4 触发清理时被删除；剩 09-02.log 与 09-03.log）。
        var archives = Directory.GetFiles(_dir, "app.*.log").ToList();
        Assert.Equal(2, archives.Count);
        Assert.Contains(archives, p => p.EndsWith("app.2026-09-02.log"));
        Assert.Contains(archives, p => p.EndsWith("app.2026-09-03.log"));
        Assert.Contains("d4", ReadAll(w.ActivePath));
    }

    [Fact]
    public void Append_NoTimestamp_UsesCurrentTime_NoRotation()
    {
        using var w = new LogFileWriter(_dir);
        w.Append("now");
        Assert.True(File.Exists(w.ActivePath));
        Assert.Contains("now", ReadAll(w.ActivePath));
    }

    [Fact]
    public void Dispose_Twice_IsSafe()
    {
        var w = new LogFileWriter(_dir);
        w.Dispose();
        w.Dispose(); // 幂等释放不抛异常
        Assert.True(true);
    }
}