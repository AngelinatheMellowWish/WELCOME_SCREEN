using Object1688.Shared.Input;

namespace Object1688.Tests;

/// <summary>
/// 全局热键字符串解析测试（F-12/F-20/AC-71）。
/// 契约点：修饰键（Ctrl/Alt/Shift/Win）+ 主键（A-Z/0-9/F1-F12/命名键）；
/// 至少一个修饰键与一个主键；非法/缺修饰/多主键返回 false。
/// </summary>
public class HotkeyParserTests
{
    [Fact]
    public void Parse_DefaultCtrlAltO_IsCaptured()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+O", out var spec));
        Assert.Equal(HotkeySpec.ModControl | HotkeySpec.ModAlt, spec.Modifiers);
        Assert.Equal((uint)'O', spec.VirtualKey);
        Assert.Equal("Ctrl+Alt+O", spec.Normalized);
    }

    [Fact]
    public void Parse_DefaultAltF_IsCaptured()
    {
        Assert.True(HotkeyParser.TryParse("Alt+F", out var spec));
        Assert.Equal(HotkeySpec.ModAlt, spec.Modifiers);
        Assert.Equal((uint)'F', spec.VirtualKey);
        Assert.Equal("Alt+F", spec.Normalized);
    }

    [Fact]
    public void Parse_IsCaseInsensitive_AndNormalizes()
    {
        Assert.True(HotkeyParser.TryParse("ctrl+alt+o", out var spec));
        Assert.Equal(HotkeySpec.ModControl | HotkeySpec.ModAlt, spec.Modifiers);
        Assert.Equal("Ctrl+Alt+O", spec.Normalized);
    }

    [Fact]
    public void Parse_FunctionKey_MapsToVirtualKey()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Shift+F1", out var spec));
        Assert.Equal(HotkeySpec.ModControl | HotkeySpec.ModShift, spec.Modifiers);
        Assert.Equal(0x70u, spec.VirtualKey);
    }

    [Fact]
    public void Parse_DigitAndNamedKeys_AreSupported()
    {
        Assert.True(HotkeyParser.TryParse("Alt+1", out var digit));
        Assert.Equal((uint)'1', digit.VirtualKey);

        Assert.True(HotkeyParser.TryParse("Win+Space", out var space));
        Assert.Equal(HotkeySpec.ModWin, space.Modifiers);
        Assert.Equal(0x20u, space.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("O")]            // 无修饰键
    [InlineData("Ctrl")]         // 无主键
    [InlineData("Ctrl+Alt+OO")]  // 多主键
    [InlineData("Ctrl+F13")]     // F 键越界
    [InlineData("Ctrl+Alt+中")]   // 非法主键
    public void Parse_Invalid_ReturnsFalse(string? text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _));
    }
}
