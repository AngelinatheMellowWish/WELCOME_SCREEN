using System.Globalization;
using Object1688.Shared.Text;

namespace Object1688.Tests;

/// <summary>
/// 占位符替换器测试（架构 §3.2/§5.7：{appName}/{time}）。
/// 契约点：未启用/未匹配时保留原文、timeFormat 各格式、未知格式抛 FormatException。
/// </summary>
public class PlaceholderResolverTests
{
    [Fact]
    public void Resolve_NullText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PlaceholderResolver.Resolve(null, "app", null));
    }

    [Fact]
    public void Resolve_EmptyText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PlaceholderResolver.Resolve("", "app", null));
    }

    [Fact]
    public void Resolve_AppNameReplacesToken()
    {
        var result = PlaceholderResolver.Resolve("{appName} 已启动", "notepad", null);
        Assert.Equal("notepad 已启动", result);
    }

    [Fact]
    public void Resolve_AppNameNull_KeepsTokenLiteral()
    {
        var result = PlaceholderResolver.Resolve("{appName} 已启动", null, null);
        Assert.Equal("{appName} 已启动", result);
    }

    [Fact]
    public void Resolve_AppNameEmpty_KeepsTokenLiteral()
    {
        var result = PlaceholderResolver.Resolve("{appName} 已启动", "", null);
        Assert.Equal("{appName} 已启动", result);
    }

    [Fact]
    public void Resolve_TimeTokenUsesTriggerTime()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 12, 34, 56, DateTimeKind.Local));
        var result = PlaceholderResolver.Resolve("现在是 {time}", null, time, "HH:mm:ss");
        Assert.Equal("现在是 12:34:56", result);
    }

    [Fact]
    public void Resolve_NoTimeToken_DoesNotEvaluateTime()
    {
        // {time} 不存在时不进入求值分支（triggerTime 为 null 也不崩溃）
        var result = PlaceholderResolver.Resolve("没有时间占位符", null, null);
        Assert.Equal("没有时间占位符", result);
    }

    [Fact]
    public void Resolve_MultipleTokens_AllReplaced()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 8, 5, 0, DateTimeKind.Local));
        var result = PlaceholderResolver.Resolve("{appName} @ {time}", "calc", time, "HH:mm");
        Assert.Equal("calc @ 08:05", result);
    }

    [Fact]
    public void FormatTime_HhMm_UsesInvariant24Hour()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 21, 7, 0, DateTimeKind.Local));
        Assert.Equal("21:07", PlaceholderResolver.FormatTime(time, "HH:mm"));
    }

    [Fact]
    public void FormatTime_HhMmSs_IncludesSeconds()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 9, 8, 7, DateTimeKind.Local));
        Assert.Equal("09:08:07", PlaceholderResolver.FormatTime(time, "HH:mm:ss"));
    }

    [Fact]
    public void FormatTime_Datetime_IncludesDateAndTime()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 18, 30, 0, DateTimeKind.Local));
        Assert.Equal("2026-09-09 18:30", PlaceholderResolver.FormatTime(time, "datetime"));
    }

    [Fact]
    public void FormatTime_24h_Forces24Hour()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 15, 45, 0, DateTimeKind.Local));
        Assert.Equal("15:45", PlaceholderResolver.FormatTime(time, "24h"));
    }

    [Fact]
    public void FormatTime_UnknownFormat_ThrowsFormatException()
    {
        var time = DateTimeOffset.Now;
        Assert.Throws<FormatException>(() => PlaceholderResolver.FormatTime(time, "bogus"));
    }

    [Fact]
    public void FormatTime_Auto_FollowsCurrentCulture()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 14, 20, 0, DateTimeKind.Local));
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("zh-CN");
            Assert.Equal("14:20", PlaceholderResolver.FormatTime(time, "auto"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void FormatTime_EmptyFormat_BehavesAsAuto()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 14, 20, 0, DateTimeKind.Local));
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("zh-CN");
            Assert.Equal("14:20", PlaceholderResolver.FormatTime(time, ""));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void FormatTime_CaseInsensitiveFormat()
    {
        var time = new DateTimeOffset(new DateTime(2026, 9, 9, 9, 30, 0, DateTimeKind.Local));
        Assert.Equal("09:30", PlaceholderResolver.FormatTime(time, "HH:MM"));
    }
}