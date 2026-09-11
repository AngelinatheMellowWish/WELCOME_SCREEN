using Object1688.Shared.Cli;

namespace Object1688.Tests;

/// <summary>
/// CliParser 解析测试（架构 §8.7 / AC-37）。
/// 支持：--config、--lang、--debug、--no-autostart、--version；非法/缺值收集 CFG-V-1003 错误。
/// </summary>
public class CliParserTests
{
    [Fact]
    public void Parse_NullArgs_ReturnsDefaults()
    {
        var options = CliParser.Parse(null);
        Assert.True(options.IsValid);
        Assert.Null(options.ConfigPath);
        Assert.Null(options.Language);
        Assert.False(options.Debug);
        Assert.False(options.NoAutostart);
        Assert.False(options.ShowVersion);
        Assert.Empty(options.Errors);
    }

    [Fact]
    public void Parse_EmptyArgs_ReturnsDefaults()
    {
        var options = CliParser.Parse([]);
        Assert.True(options.IsValid);
        Assert.Null(options.ConfigPath);
        Assert.Empty(options.Errors);
    }

    [Fact]
    public void Parse_ConfigPath_IsCaptured()
    {
        var options = CliParser.Parse(["--config", @"C:\cfg\app.json"]);
        Assert.True(options.IsValid);
        Assert.Equal(@"C:\cfg\app.json", options.ConfigPath);
    }

    [Fact]
    public void Parse_ConfigMissingValue_ReportsError()
    {
        var options = CliParser.Parse(["--config"]);
        Assert.False(options.IsValid);
        Assert.Single(options.Errors);
        Assert.Contains("--config", options.Errors[0]);
    }

    [Fact]
    public void Parse_ConfigFollowedByFlag_ReportsMissingValue()
    {
        // 值以 "--" 开头 → 视为缺值而非取参（防吞掉后续参数）
        var options = CliParser.Parse(["--config", "--debug"]);
        Assert.False(options.IsValid);
        Assert.Null(options.ConfigPath);
        Assert.True(options.Debug);
        Assert.Contains(options.Errors, e => e.Contains("--config"));
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    public void Parse_ValidLang_IsAccepted(string lang)
    {
        var options = CliParser.Parse(["--lang", lang]);
        Assert.True(options.IsValid);
        Assert.Equal(lang, options.Language);
    }

    [Theory]
    [InlineData("ZH-cn")]
    [InlineData("EN-us")]
    public void Parse_Lang_IsCaseInsensitive(string lang)
    {
        var options = CliParser.Parse(["--lang", lang]);
        Assert.True(options.IsValid);
        Assert.Equal(lang, options.Language);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("zh")]
    [InlineData("")]
    public void Parse_InvalidLang_ReportsError(string lang)
    {
        var options = CliParser.Parse(["--lang", lang]);
        Assert.False(options.IsValid);
        Assert.Null(options.Language);
        Assert.Contains(options.Errors, e => e.Contains("--lang"));
    }

    [Fact]
    public void Parse_LangMissingValue_ReportsError()
    {
        var options = CliParser.Parse(["--lang"]);
        Assert.False(options.IsValid);
        Assert.Single(options.Errors);
        Assert.Contains("--lang", options.Errors[0]);
    }

    [Fact]
    public void Parse_DebugFlag_IsCaptured()
    {
        var options = CliParser.Parse(["--debug"]);
        Assert.True(options.IsValid);
        Assert.True(options.Debug);
    }

    [Fact]
    public void Parse_NoAutostartFlag_IsCaptured()
    {
        var options = CliParser.Parse(["--no-autostart"]);
        Assert.True(options.IsValid);
        Assert.True(options.NoAutostart);
    }

    [Fact]
    public void Parse_VersionFlag_IsCaptured()
    {
        var options = CliParser.Parse(["--version"]);
        Assert.True(options.IsValid);
        Assert.True(options.ShowVersion);
    }

    [Fact]
    public void Parse_QuitFlag_IsCaptured()
    {
        var options = CliParser.Parse(["--quit"]);
        Assert.True(options.IsValid);
        Assert.True(options.QuitRequested);
    }

    [Fact]
    public void Parse_UnknownArgument_ReportsError()
    {
        var options = CliParser.Parse(["--bogus"]);
        Assert.False(options.IsValid);
        Assert.Contains(options.Errors, e => e.Contains("--bogus"));
    }

    [Fact]
    public void Parse_MixedValidArgs_AllCaptured()
    {
        var options = CliParser.Parse(
        [
            "--config", @"D:\o1688\config.json",
            "--lang", "en-US",
            "--debug",
            "--no-autostart",
        ]);
        Assert.True(options.IsValid);
        Assert.Equal(@"D:\o1688\config.json", options.ConfigPath);
        Assert.Equal("en-US", options.Language);
        Assert.True(options.Debug);
        Assert.True(options.NoAutostart);
        Assert.Empty(options.Errors);
    }

    [Fact]
    public void Parse_MixedWithErrors_KeepsValidParts()
    {
        var options = CliParser.Parse(
        [
            "--config", @"x.json",
            "--lang", "xx-XX",
            "--version",
        ]);
        Assert.False(options.IsValid);
        Assert.Equal(@"x.json", options.ConfigPath);
        Assert.True(options.ShowVersion);
        Assert.Contains(options.Errors, e => e.Contains("--lang"));
    }

    [Fact]
    public void BuildUsageText_ContainsAllOptions()
    {
        var text = CliParser.BuildUsageText();
        Assert.Contains("--config", text);
        Assert.Contains("--lang", text);
        Assert.Contains("--debug", text);
        Assert.Contains("--no-autostart", text);
        Assert.Contains("--version", text);
        Assert.Contains("--quit", text);
        Assert.Contains("--control", text);
    }

    [Fact]
    public void Parse_ControlWithTextAndScreen_IsCaptured()
    {
        var options = CliParser.Parse(["--control", "banner", "--text", "Hello 世界", "--screen", "2"]);
        Assert.True(options.IsValid);
        Assert.Equal("banner", options.ControlCommand);
        Assert.Equal("Hello 世界", options.ControlText);
        Assert.Equal("2", options.ControlScreen);
    }

    [Fact]
    public void Parse_ControlMissingValue_ReportsError()
    {
        var options = CliParser.Parse(["--control"]);
        Assert.False(options.IsValid);
        Assert.Contains(options.Errors, e => e.Contains("--control"));
    }

    [Fact]
    public void Parse_ControlDefaults_NullTextScreen()
    {
        var options = CliParser.Parse(["--control", "status"]);
        Assert.True(options.IsValid);
        Assert.Equal("status", options.ControlCommand);
        Assert.Null(options.ControlText);
        Assert.Null(options.ControlScreen);
    }
}