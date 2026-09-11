using Object1688.Shared.Config;
using Object1688.Shared.Monitor;
using Object1688.Shared.Text;

namespace Object1688.Tests;

/// <summary>
/// DndGate 勿扰/暂停裁决测试（需求书 F-25/AC-31/AC-90）。
/// </summary>
public class DndGateTests
{
    private static DateTimeOffset LocalAt(string ymd, string hhmm)
    {
        var date = DateTime.ParseExact(ymd, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var time = TimeOnly.ParseExact(hhmm, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var local = date.Date + time.ToTimeSpan();
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    [Fact]
    public void Paused_SuppressesAutoTrigger()
    {
        var dnd = new DndConfig { Paused = true, ScheduleEnabled = false, Schedule = [] };
        Assert.True(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false));
    }

    [Fact]
    public void Paused_DoesNotSuppressForcedPath()
    {
        var dnd = new DndConfig { Paused = true, ScheduleEnabled = false, Schedule = [] };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: true));
    }

    [Fact]
    public void NoSchedule_NotSuppressed()
    {
        var dnd = new DndConfig { Paused = false, ScheduleEnabled = false, Schedule = [] };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false));
    }

    [Fact]
    public void PresentationAutoSilence_On_SuppressesWhenPresentationActive()
    {
        var dnd = new DndConfig { Paused = false, ScheduleEnabled = false, PresentationAutoSilence = true, Schedule = [] };
        Assert.True(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false, presentationActive: true));
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false, presentationActive: false));
    }

    [Fact]
    public void PresentationAutoSilence_Off_NotSuppressed()
    {
        var dnd = new DndConfig { Paused = false, ScheduleEnabled = false, PresentationAutoSilence = false, Schedule = [] };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false, presentationActive: true));
    }

    [Fact]
    public void PresentationAutoSilence_ForcedPath_NotSuppressed()
    {
        var dnd = new DndConfig { Paused = false, ScheduleEnabled = false, PresentationAutoSilence = true, Schedule = [] };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: true, presentationActive: true));
    }

    [Fact]
    public void Schedule_InsideWindow_Suppressed()
    {
        // 2026-09-10 是周四 (ISO 4)
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "09:00", End = "18:00" }],
        };
        Assert.True(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false));
    }

    [Fact]
    public void Schedule_OutsideWindow_NotSuppressed()
    {
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "09:00", End = "18:00" }],
        };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "20:00"), isForced: false));
    }

    [Fact]
    public void Schedule_WrongWeekday_NotSuppressed()
    {
        // 2026-09-11 是周五 (ISO 5)，时段仅周四
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "00:00", End = "23:59" }],
        };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-11", "12:00"), isForced: false));
    }

    [Fact]
    public void Schedule_CrossMidnight_EarlyMorningSuppressed()
    {
        // 时段 22:00 → 02:00（跨午夜）：凌晨 01:00 应抑制
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "22:00", End = "02:00" }],
        };
        Assert.True(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "01:00"), isForced: false));
    }

    [Fact]
    public void Schedule_CrossMidnight_LateNightSuppressed()
    {
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "22:00", End = "02:00" }],
        };
        Assert.True(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "23:30"), isForced: false));
    }

    [Fact]
    public void Schedule_CrossMidnight_AfternoonNotSuppressed()
    {
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "22:00", End = "02:00" }],
        };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false));
    }

    [Fact]
    public void Schedule_InvalidTime_NotSuppressed()
    {
        var dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = true,
            Schedule = [new DndScheduleEntry { Days = [4], Start = "not-a-time", End = "18:00" }],
        };
        Assert.False(DndGate.IsSuppressed(dnd, LocalAt("2026-09-10", "12:00"), isForced: false));
    }

    [Fact]
    public void IsoDayOfWeek_ThursdayIs4()
    {
        Assert.Equal(4, DndGate.IsoDayOfWeek(DayOfWeek.Thursday));
        Assert.Equal(7, DndGate.IsoDayOfWeek(DayOfWeek.Sunday));
        Assert.Equal(1, DndGate.IsoDayOfWeek(DayOfWeek.Monday));
    }
}

/// <summary>BannerAssembler.ComposeManual 手动大字组装测试（F-12/AC-29）。</summary>
public class ComposeManualTests
{
    private static readonly GlobalConfig Global = new()
    {
        DefaultFontSize = 96,
        DefaultPosition = "center",
        DefaultOutlineColor = "#000000",
        DefaultOutlineWidth = 0,
        TargetScreen = "primary",
        TimeFormat = "auto",
    };

    private static readonly SoundConfig SoundOff = new()
    {
        Enabled = false,
        Source = "system",
        CustomPath = string.Empty,
        Volume = 80,
    };

    [Fact]
    public void ComposeManual_UsesManualDisplayFields()
    {
        var manual = new ManualConfig
        {
            Enabled = true,
            DisplayLines = new[] { new DisplayLine { Text = "休息一下", FontSize = 120 } },
            DelaySeconds = 0,
            HoldSeconds = 6,
            WrapStrategy = WrapStrategy.Wrap,
            Position = "top-center",
            FontSize = 120,
            Align = TextAlignment.Center,
            OutlineColor = "#FF0000",
            OutlineWidth = 3,
            TargetScreen = "2",
            Shortcut = "Ctrl+Alt+O",
        };

        var req = BannerAssembler.ComposeManual(manual, Global, SoundOff);

        Assert.Equal("休息一下", req.DisplayLines[0].Text);
        Assert.Equal(120, req.FontSize);
        Assert.Equal("top-center", req.Position);
        Assert.Equal("#FF0000", req.OutlineColor);
        Assert.Equal(3, req.OutlineWidth);
        Assert.Equal("2", req.TargetScreen);
        Assert.Equal(6, req.HoldSeconds);
        Assert.False(req.PlaySound);
    }

    [Fact]
    public void ComposeManual_UnsetFields_FallBackToGlobal()
    {
        var manual = new ManualConfig
        {
            Enabled = true,
            DisplayLines = new[] { new DisplayLine { Text = "Object1688", FontSize = 0 } },
            DelaySeconds = 0,
            HoldSeconds = 4,
            WrapStrategy = WrapStrategy.Wrap,
            Position = string.Empty,      // 未配置 → 全局 center
            FontSize = 0,                 // 未配置 → 全局 96
            Align = TextAlignment.Center,
            OutlineColor = string.Empty,  // 未配置 → 全局
            OutlineWidth = -1,            // 未配置 → 全局
            TargetScreen = string.Empty,  // 未配置 → 全局 primary
            Shortcut = null,
        };

        var req = BannerAssembler.ComposeManual(manual, Global, SoundOff);

        Assert.Equal(96, req.FontSize);
        Assert.Equal("center", req.Position);
        Assert.Equal("#000000", req.OutlineColor);
        Assert.Equal(0, req.OutlineWidth);
        Assert.Equal("primary", req.TargetScreen);
    }
}
