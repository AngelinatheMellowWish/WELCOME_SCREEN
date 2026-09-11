using System.Globalization;
using Object1688.Shared.I18n;

namespace Object1688.Tests;

/// <summary>
/// LocalizedStrings 本地化回退测试（开发规范 §2.6 / AC-75：zh/en 键一致 + 运行回退 GEN-W-9003）。
/// </summary>
public class LocalizedStringsTests
{
    [Fact]
    public void Get_ChineseCulture_ReturnsSimplifiedChinese()
    {
        var text = LocalizedStrings.Get("TraySettings", new CultureInfo("zh-Hans"));
        Assert.Equal("设置…", text);
    }

    [Fact]
    public void Get_EnglishCulture_ReturnsEnglish()
    {
        var text = LocalizedStrings.Get("TraySettings", new CultureInfo("en-US"));
        Assert.Equal("Settings…", text);
    }

    [Theory]
    [InlineData("TrayManual")]
    [InlineData("TrayOpenLog")]
    [InlineData("TrayCopyLastError")]
    [InlineData("StatusPaused")]
    [InlineData("StatusDnd")]
    [InlineData("ConfigWindowTitle")]
    [InlineData("PerfWindowTitle")]
    [InlineData("AboutWindowTitle")]
    public void Get_NewTrayKeys_ResolveInBothLanguages(string key)
    {
        var zh = LocalizedStrings.Get(key, new CultureInfo("zh-Hans"));
        var en = LocalizedStrings.Get(key, new CultureInfo("en-US"));

        Assert.NotEqual($"[{key}]", zh); // 非占位（键已登记）
        Assert.NotEqual($"[{key}]", en);
    }

    [Fact]
    public void Get_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => LocalizedStrings.Get(string.Empty));
    }

    [Fact]
    public void Get_MissingKey_TriggersGenW9003Handler_AndReturnsPlaceholder()
    {
        var reported = new List<string>();
        LocalizedStrings.MissingKeyHandler = reported.Add;
        try
        {
            var text = LocalizedStrings.Get("NoSuchKey_ForTest", new CultureInfo("en-US"));
            Assert.Equal("[NoSuchKey_ForTest]", text);
            Assert.Contains("NoSuchKey_ForTest", reported);
        }
        finally
        {
            LocalizedStrings.MissingKeyHandler = null;
        }
    }

    [Fact]
    public void Get_ExistingKeys_DoNotTriggerHandler()
    {
        var reported = new List<string>();
        LocalizedStrings.MissingKeyHandler = reported.Add;
        try
        {
            _ = LocalizedStrings.Get("AppDisplayName", new CultureInfo("zh-CN"));
            _ = LocalizedStrings.Get("ErrorBalloonTitle", new CultureInfo("en-US"));
            Assert.Empty(reported);
        }
        finally
        {
            LocalizedStrings.MissingKeyHandler = null;
        }
    }
}