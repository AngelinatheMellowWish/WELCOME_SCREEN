using Object1688.Shared.Perf;
using Object1688.Shared.Stats;

namespace Object1688.Tests;

/// <summary>ProcessProbe CPU 差值计算测试（架构 §6.1）。</summary>
public class ProcessProbeTests
{
    [Theory]
    [InlineData(0, 10, 10, 100.0)]     // 满核：10s CPU / 10s 墙钟 → 100%（单核归一化）
    [InlineData(0, 5, 10, 50.0)]       // 半核
    [InlineData(0, 0, 10, 0.0)]        // 空闲
    public void ComputeCpuPercent_CorrectForCoreUsage(double prior, double current, double elapsed, double expected)
    {
        var pct = ProcessProbe.ComputeCpuPercent(prior, current, elapsed);
        Assert.Equal(expected, pct, 1);
    }

    [Fact]
    public void ComputeCpuPercent_ZeroElapsed_ReturnsZero()
    {
        Assert.Equal(0, ProcessProbe.ComputeCpuPercent(0, 5, 0));
    }

    [Fact]
    public void ComputeCpuPercent_ClockRollback_ReturnsZero()
    {
        Assert.Equal(0, ProcessProbe.ComputeCpuPercent(10, 5, 1));
    }

    [Fact]
    public void ComputeCpuPercent_CapsAtCoreCountPercent()
    {
        // 多核：100%×核数 是上限；超量输入被钳制
        var pct = ProcessProbe.ComputeCpuPercent(0, 1000, 1);
        Assert.True(pct <= 100.0 * ProcessProbe.ProcessorCount + 0.001);
    }
}

/// <summary>
/// M4 性能监控纯逻辑单测：RingBuffer 环形、StatsTracker 统计聚合、PerfExporter 导出。
/// </summary>
public class PerfRingBufferTests
{
    [Fact]
    public void Append_WithinCapacity_KeepsAllInOrder()
    {
        var buf = new RingBuffer<int>(3);
        buf.Append(1);
        buf.Append(2);
        buf.Append(3);
        Assert.Equal(new[] { 1, 2, 3 }, buf.Snapshot());
    }

    [Fact]
    public void Append_OverCapacity_EvictsOldest()
    {
        var buf = new RingBuffer<int>(3);
        buf.Append(1);
        buf.Append(2);
        buf.Append(3);
        buf.Append(4);
        buf.Append(5);
        Assert.Equal(new[] { 3, 4, 5 }, buf.Snapshot());
        Assert.Equal(3, buf.Count);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var buf = new RingBuffer<int>(3);
        buf.Append(1);
        buf.Clear();
        Assert.Empty(buf.Snapshot());
        Assert.Equal(0, buf.Count);
    }

    [Fact]
    public void Capacity_ReturnsConfigured()
    {
        Assert.Equal(500, new RingBuffer<int>(500).Capacity);
    }
}

/// <summary>StatsTracker 统计聚合/持久化测试（架构 §6.4 / AC-47）。</summary>
public class StatsTrackerTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), "Object1688.StatsTrackerTests", Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
            // 测试清理不阻断结论
        }
    }

    [Fact]
    public void Record_AccumulatesTotalPerRuleAndHour()
    {
        var tracker = new StatsTracker(_path, flushInterval: TimeSpan.FromDays(1));
        var t = new DateTimeOffset(2026, 9, 10, 12, 30, 0, TimeSpan.FromHours(8)); // 本地 12 时
        tracker.Record("r-001", t);
        tracker.Record("r-001", t.AddMinutes(1));
        tracker.Record("r-002", t.AddMinutes(2));

        var data = tracker.Data;
        Assert.Equal(3, data.TotalTriggers);
        Assert.Equal(2, data.PerRule["r-001"].Count);
        Assert.Equal(1, data.PerRule["r-002"].Count);
        // 本地时区小时分布（12 时）
        var hourKey = t.ToLocalTime().Hour.ToString();
        Assert.Equal(3, data.ByHour[hourKey]);
    }

    [Fact]
    public void Record_AfterFlushInterval_PersistsToDisk()
    {
        // 短节流 → Record 触发落盘
        var tracker = new StatsTracker(_path, flushInterval: TimeSpan.Zero);
        tracker.Record("r-001", DateTimeOffset.UtcNow);
        Assert.True(File.Exists(_path));
        var reloaded = StatsFile.Load(_path);
        Assert.Equal(1, reloaded.Data.TotalTriggers);
    }

    [Fact]
    public void Reset_ClearsAndPersistsEmpty()
    {
        var tracker = new StatsTracker(_path, flushInterval: TimeSpan.Zero);
        tracker.Record("r-001", DateTimeOffset.UtcNow);
        tracker.Reset();

        var reloaded = StatsFile.Load(_path);
        Assert.Equal(0, reloaded.Data.TotalTriggers);
        Assert.Empty(reloaded.Data.PerRule);
    }

    [Fact]
    public void Flush_ForcesPendingWrite()
    {
        var tracker = new StatsTracker(_path, flushInterval: TimeSpan.FromDays(1));
        tracker.Record("r-001", DateTimeOffset.UtcNow);
        // 节流未到：磁盘内容仍是 Load 时创建的初始空统计（TotalTriggers=0）
        Assert.Equal(0, StatsFile.Load(_path).Data.TotalTriggers);
        tracker.Flush();
        Assert.Equal(1, StatsFile.Load(_path).Data.TotalTriggers);
    }
}

/// <summary>PerfExporter 导出测试（架构 §6.3 / AC-91）。</summary>
public class PerfExporterTests : IDisposable{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "Object1688.PerfExporterTests", Guid.NewGuid().ToString("N"));

    public PerfExporterTests() => Directory.CreateDirectory(_dir);

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

    private static readonly DateTimeOffset Fixed = new(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void ExportPerfCsv_WritesBomHeaderAndRows()
    {
        var samples = new[]
        {
            new PerfSample(Fixed, "main", 12.5, 1024),
            new PerfSample(Fixed.AddSeconds(1), "overlay", 8.0, 2048),
        };

        var path = PerfExporter.ExportPerfCsv(samples, _dir);

        Assert.True(File.Exists(path));
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF); // UTF-8 BOM
        var text = File.ReadAllText(path);
        Assert.StartsWith("timestamp,processTag,cpuPercent,workingSetBytes", text);
        Assert.Contains("main", text);
        Assert.Contains("12.5", text);
    }

    [Fact]
    public void ExportStatsJson_RoundTripsViaStatsFile()
    {
        var data = new StatsData { TotalTriggers = 7 };
        data.PerRule["r-1"] = new RuleStat { Count = 7, LastTriggerAt = Fixed };

        var path = PerfExporter.ExportStatsJson(data, _dir);

        var loaded = StatsFile.Load(path);
        Assert.Equal(7, loaded.Data.TotalTriggers);
        Assert.Equal(7, loaded.Data.PerRule["r-1"].Count);
    }

    [Fact]
    public void ExportEventsCsv_EscapesCommaInText()
    {
        var events = new[] { new EventItem(Fixed, "规则 a,b 命中", "MON-I-5003") };
        var path = PerfExporter.ExportEventsCsv(events, _dir);
        var text = File.ReadAllText(path);
        Assert.Contains("\"规则 a,b 命中\"", text);
    }

    [Fact]
    public void ExportEventsTxt_WritesTimestampedLines()
    {
        var events = new[] { new EventItem(Fixed, "命中 游戏", "MON-I-5003") };
        var path = PerfExporter.ExportEventsTxt(events, _dir);
        var text = File.ReadAllText(path);
        Assert.Contains("[MON-I-5003]", text);
        Assert.Contains("命中 游戏", text);
    }

    [Fact]
    public void Export_SameSecond_AppendsSequenceSuffix()
    {
        var events = new[] { new EventItem(Fixed, "a") };
        var p1 = PerfExporter.ExportEventsTxt(events, _dir);
        var p2 = PerfExporter.ExportEventsTxt(events, _dir);
        Assert.NotEqual(p1, p2); // 同秒 → 序号防覆盖
        Assert.True(File.Exists(p1));
        Assert.True(File.Exists(p2));
    }
}
