using System.Text.Json;
using Object1688.Shared;
using Object1688.Shared.Ipc;
using Object1688.Shared.Perf;

namespace Object1688.Tests;

/// <summary>
/// 性能监控数据契约测试（AC-68 帧率 / AC-92 最近一次大字回看）。
/// 契约点：FrameRateReport / LastBannerReport JSON round-trip；MonitorFeed 追加与读写。
/// </summary>
public class PerfFeedTests
{
    [Fact]
    public void FrameRateReport_JsonRoundTrips()
    {
        var json = JsonSerializer.Serialize(new FrameRateReport { Fps = 59.5, Degraded = true }, IpcJson.Options);

        var back = JsonSerializer.Deserialize<FrameRateReport>(json, IpcJson.Options)!;

        Assert.Equal(59.5, back.Fps);
        Assert.True(back.Degraded);
        Assert.Contains("\"fps\"", json);
        Assert.Contains("\"degraded\"", json);
    }

    [Fact]
    public void LastBannerReport_JsonRoundTrips()
    {
        var original = new LastBannerReport
        {
            Lines = new[] { "浏览器 Browser", "新标签页" },
            RuleId = "r-001",
            Timestamp = DateTimeOffset.UtcNow,
        };

        var json = JsonSerializer.Serialize(original, IpcJson.Options);
        var back = JsonSerializer.Deserialize<LastBannerReport>(json, IpcJson.Options)!;

        Assert.Equal(2, back.Lines.Count);
        Assert.Equal("浏览器 Browser", back.Lines[0]);
        Assert.Equal("r-001", back.RuleId);
    }

    [Fact]
    public void MonitorFeed_StoresFrameRates_AndLastBanner()
    {
        var feed = new MonitorFeed();

        feed.AddFrameRate(60, false);
        feed.AddFrameRate(42, true);

        var points = feed.FrameRates.Snapshot();
        Assert.Equal(2, points.Count);
        Assert.Equal(60, points[0].Fps);
        Assert.True(points[1].Degraded);

        Assert.Null(feed.LastBanner);
        feed.LastBanner = new LastBannerReport { Lines = new[] { "x" }, Timestamp = DateTimeOffset.UtcNow };
        Assert.NotNull(feed.LastBanner);
        Assert.Equal("x", feed.LastBanner!.Lines[0]);
    }

    [Fact]
    public void SystemEventReport_JsonRoundTrips()
    {
        var json = JsonSerializer.Serialize(new SystemEventReport { Kind = SystemEventKind.Lock }, IpcJson.Options);

        var back = JsonSerializer.Deserialize<SystemEventReport>(json, IpcJson.Options)!;

        Assert.Equal(SystemEventKind.Lock, back.Kind);
        Assert.Contains("Lock", json); // 枚举名即协议字符串
    }

    [Fact]
    public void PresentationStateReport_JsonRoundTrips()
    {
        var json = JsonSerializer.Serialize(new PresentationStateReport { Active = true }, IpcJson.Options);

        var back = JsonSerializer.Deserialize<PresentationStateReport>(json, IpcJson.Options)!;

        Assert.True(back.Active);
    }

    [Fact]
    public void RenderCostReport_JsonRoundTrips()
    {
        var json = JsonSerializer.Serialize(new RenderCostReport { CpuMs = 123.4, MemMB = 8.5 }, IpcJson.Options);

        var back = JsonSerializer.Deserialize<RenderCostReport>(json, IpcJson.Options)!;

        Assert.Equal(123.4, back.CpuMs);
        Assert.Equal(8.5, back.MemMB);
    }

    [Fact]
    public void MonitorFeed_StoresRenderCosts()
    {
        var feed = new MonitorFeed();
        feed.AddRenderCost(150, 12.5);

        var costs = feed.RenderCosts.Snapshot();

        var single = Assert.Single(costs);
        Assert.Equal(150, single.CpuMs);
        Assert.Equal(12.5, single.MemMB);
    }

    [Fact]
    public void WarningReport_JsonRoundTrips()
    {
        var json = JsonSerializer.Serialize(
            new WarningReport { ErrorCode = ErrorCodes.OverlaySecureDesktopBlocked, Message = "无法覆盖" },
            IpcJson.Options);

        var back = JsonSerializer.Deserialize<WarningReport>(json, IpcJson.Options)!;

        Assert.Equal(ErrorCodes.OverlaySecureDesktopBlocked, back.ErrorCode);
        Assert.Equal("无法覆盖", back.Message);
    }

    [Fact]
    public void FrameRateTracker_InitialState_HasNoValue()
    {
        var tracker = new FrameRateTracker();

        Assert.False(tracker.HasValue);
        Assert.Equal(-1, tracker.Fps);
        Assert.False(tracker.Degraded);
    }

    [Fact]
    public void FrameRateTracker_Update_StoresLatestSample()
    {
        var tracker = new FrameRateTracker();

        tracker.Update(60.0, false);
        Assert.True(tracker.HasValue);
        Assert.Equal(60.0, tracker.Fps);
        Assert.False(tracker.Degraded);

        tracker.Update(38.5, true);
        Assert.Equal(38.5, tracker.Fps);
        Assert.True(tracker.Degraded);
    }
}
