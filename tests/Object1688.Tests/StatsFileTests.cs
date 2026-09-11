using Object1688.Shared.Stats;

namespace Object1688.Tests;

/// <summary>
/// StatsFile 自愈测试（架构 §6.4 / AC-69：损坏 → .corrupt 备份 + 重建默认）。
/// </summary>
public class StatsFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "Object1688.StatsTests",
        Guid.NewGuid().ToString("N"));

    private string PathFor(string name = "stats.json") => Path.Combine(_dir, name);

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
    public void Load_MissingFile_CreatesDefault_NotHealed()
    {
        var result = StatsFile.Load(PathFor());

        Assert.NotNull(result.Data);
        Assert.False(result.Healed);
        Assert.True(File.Exists(PathFor()));
        Assert.Equal(StatsFile.SchemaVersion, result.Data.SchemaVersion);
    }

    [Fact]
    public void Load_ValidJson_ParsesContent()
    {
        Directory.CreateDirectory(_dir);
        var data = new StatsData { TotalTriggers = 42 };
        data.PerRule["r-001"] = new RuleStat { Count = 7 };
        Assert.True(StatsFile.TrySave(data, PathFor()));

        var result = StatsFile.Load(PathFor());

        Assert.False(result.Healed);
        Assert.Equal(42, result.Data.TotalTriggers);
        Assert.Equal(7, result.Data.PerRule["r-001"].Count);
    }

    [Fact]
    public void Load_CorruptJson_BacksUpToCorrupt_AndRebuilds()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathFor(), "{ 这不是合法 JSON !!!");

        var result = StatsFile.Load(PathFor());

        Assert.True(result.Healed);
        Assert.Equal(StatsFile.SchemaVersion, result.Data.SchemaVersion);
        Assert.True(File.Exists(PathFor() + ".corrupt"));
        Assert.Contains("这不是合法 JSON", File.ReadAllText(PathFor() + ".corrupt"));
    }

    [Fact]
    public void Load_EmptyFile_IsHealed()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathFor(), string.Empty);

        var result = StatsFile.Load(PathFor());

        Assert.True(result.Healed);
        Assert.True(File.Exists(PathFor() + ".corrupt"));
    }

    [Fact]
    public void TrySave_ThenLoad_RoundTrips()
    {
        Directory.CreateDirectory(_dir);
        var data = new StatsData
        {
            TotalTriggers = 99,
            UpdatedAt = new DateTimeOffset(2026, 9, 9, 4, 0, 0, TimeSpan.Zero),
        };
        data.PerRule["r-x"] = new RuleStat { Count = 3, LastTriggerAt = new DateTimeOffset(2026, 9, 9, 4, 0, 0, TimeSpan.Zero) };
        data.ByHour["13"] = 5;

        Assert.True(StatsFile.TrySave(data, PathFor()));
        var loaded = StatsFile.Load(PathFor());

        Assert.Equal(99, loaded.Data.TotalTriggers);
        Assert.Equal(3, loaded.Data.PerRule["r-x"].Count);
        Assert.Equal(5, loaded.Data.ByHour["13"]);
        Assert.Equal(data.UpdatedAt, loaded.Data.UpdatedAt);
    }
}