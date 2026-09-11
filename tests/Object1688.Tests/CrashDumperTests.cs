using Object1688.Shared;
using Object1688.Shared.Crash;

namespace Object1688.Tests;

/// <summary>
/// CrashDumper / CrashCleanup 测试（架构 §7.2 / AC-13 / AC-52）。
/// </summary>
public class CrashDumperTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "Object1688.CrashTests",
        Guid.NewGuid().ToString("N"));

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
    public void TryWriteDump_CreatesNonEmptyDmp()
    {
        var path = CrashDumper.TryWriteDump(_dir, "main");

        Assert.NotNull(path);
        Assert.StartsWith("crash_main_", Path.GetFileName(path));
        Assert.EndsWith(".dmp", path);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void TryWriteDump_CreatesDirectoryWhenMissing()
    {
        var path = CrashDumper.TryWriteDump(_dir, "overlay");
        Assert.NotNull(path);
        Assert.True(Directory.Exists(_dir));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void TryWriteDump_ReturnsNull_WhenDirectoryCannotBeCreated()
    {
        // 在目标路径上放置同名文件 → Directory.CreateDirectory 抛 IOException → 返回 null
        var blocker = Path.Combine(_dir, "blocker");
        Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
        File.WriteAllText(blocker, "i-am-a-file");

        var path = CrashDumper.TryWriteDump(blocker, "main");
        Assert.Null(path);
    }

    [Fact]
    public void TryCleanup_RemovesOldest_WhenCountExceeds()
    {
        Directory.CreateDirectory(_dir);
        for (var i = 0; i < 5; i++)
        {
            var p = Path.Combine(_dir, $"crash_main_{i:000}.dmp");
            File.WriteAllText(p, "dump-content-" + i);
            File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddMinutes(-(5 - i)));
        }

        var ok = CrashCleanup.TryCleanup(_dir, maxFileCount: 2);

        Assert.True(ok);
        var remaining = Directory.GetFiles(_dir, "crash_*.dmp");
        Assert.Equal(2, remaining.Length);
    }

    [Fact]
    public void TryCleanup_RemovesOldest_WhenTotalBytesExceeds()
    {
        Directory.CreateDirectory(_dir);
        for (var i = 0; i < 4; i++)
        {
            var p = Path.Combine(_dir, $"crash_main_{i:000}.dmp");
            File.WriteAllBytes(p, new byte[100]); // 每个 100B
            File.SetLastWriteTimeUtc(p, DateTime.UtcNow.AddMinutes(-(4 - i)));
        }

        // 总上限 250B：保留最新 ≤250B（3 个=300 超了 → 应保留 2 个），其余删除
        var ok = CrashCleanup.TryCleanup(_dir, maxFileCount: 10, maxTotalBytes: 250);

        Assert.True(ok);
        var remaining = Directory.GetFiles(_dir, "crash_*.dmp");
        Assert.Equal(2, remaining.Length);
    }

    [Fact]
    public void TryCleanup_MissingDirectory_IsSuccess()
    {
        var ok = CrashCleanup.TryCleanup(Path.Combine(_dir, "nope"));
        Assert.True(ok);
    }

    [Fact]
    public void Handle_WritesDumpAndErrArtifacts()
    {
        var crashRoot = Path.Combine(_dir, "log", "crash");
        CrashGuard.Handle(new InvalidOperationException("模拟崩溃"), isTerminating: true, crashRoot);

        Assert.True(Directory.Exists(crashRoot));
        var dumps = Directory.GetFiles(crashRoot, "crash_*.dmp");
        var errs = Directory.GetFiles(crashRoot, "crash_*.err.txt");
        Assert.Single(dumps);
        Assert.Single(errs);
        var errText = ReadAllTextSafe(errs[0]);
        Assert.Contains(ErrorCodes.GenericCrash, errText);
        Assert.Contains("模拟崩溃", errText);
        Assert.Contains("转储文件:", errText);
    }

    private static string ReadAllTextSafe(string path)
        => System.IO.File.ReadAllText(path);
}