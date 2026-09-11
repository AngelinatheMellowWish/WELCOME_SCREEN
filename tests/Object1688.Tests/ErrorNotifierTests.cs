using Object1688.Shared.Notify;

namespace Object1688.Tests;

/// <summary>
/// ErrorNotifier 去重聚合测试（需求书 F-42 / AC-84）。
/// </summary>
public class ErrorNotifierTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Decide_FirstOccurrence_Shows()
    {
        var n = new ErrorNotifier();
        var action = n.Decide("MON-E-5001", T0);
        Assert.Equal(NotificationKind.Show, action.Kind);
    }

    [Fact]
    public void Decide_SameCodeWithinWindow_Suppressed()
    {
        var n = new ErrorNotifier();
        n.Decide("MON-E-5001", T0);
        var second = n.Decide("MON-E-5001", T0.AddSeconds(5));
        Assert.Equal(NotificationKind.Suppressed, second.Kind);
        Assert.Equal(2, second.CumulativeCount);
    }

    [Fact]
    public void Decide_DifferentCode_EachShows()
    {
        var n = new ErrorNotifier();
        Assert.Equal(NotificationKind.Show, n.Decide("MON-E-5001", T0).Kind);
        Assert.Equal(NotificationKind.Show, n.Decide("GEN-E-9001", T0).Kind);
    }

    [Fact]
    public void Decide_WindowExpiry_ShowsAgain()
    {
        var n = new ErrorNotifier();
        n.Decide("MON-E-5001", T0);
        n.Decide("MON-E-5001", T0.AddSeconds(10));
        var afterWindow = n.Decide("MON-E-5001", T0.AddSeconds(31));
        Assert.Equal(NotificationKind.Show, afterWindow.Kind);
        Assert.Equal(1, afterWindow.CumulativeCount);
    }

    [Fact]
    public void Decide_ReachesThreshold_AggregatesAndResets()
    {
        var n = new ErrorNotifier(aggregateThreshold: 5);
        n.Decide("MON-E-5001", T0);                                  // Show (1)
        n.Decide("MON-E-5001", T0.AddSeconds(1));                    // Suppressed (2)
        n.Decide("MON-E-5001", T0.AddSeconds(2));                    // Suppressed (3)
        n.Decide("MON-E-5001", T0.AddSeconds(3));                    // Suppressed (4)
        var fifth = n.Decide("MON-E-5001", T0.AddSeconds(4));        // Aggregate (5)

        Assert.Equal(NotificationKind.Aggregate, fifth.Kind);
        Assert.Equal(5, fifth.CumulativeCount);

        // 重置后新周期：再次出现从 Show 开始
        var afterReset = n.Decide("MON-E-5001", T0.AddSeconds(5));
        Assert.Equal(NotificationKind.Show, afterReset.Kind);
    }

    [Fact]
    public void Decide_NullOrEmptyCode_AlwaysShows()
    {
        var n = new ErrorNotifier();
        Assert.Equal(NotificationKind.Show, n.Decide(null, T0).Kind);
        Assert.Equal(NotificationKind.Show, n.Decide(null, T0.AddSeconds(1)).Kind);
        Assert.Equal(NotificationKind.Show, n.Decide(string.Empty, T0.AddSeconds(2)).Kind);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var n = new ErrorNotifier();
        n.Decide("MON-E-5001", T0);
        n.Reset();
        var action = n.Decide("MON-E-5001", T0.AddSeconds(5));
        Assert.Equal(NotificationKind.Show, action.Kind);
        Assert.Equal(1, action.CumulativeCount);
    }
}