using Object1688.Shared.Text;

namespace Object1688.Tests;

/// <summary>
/// 样式预设测试（架构 §5.6 表：AC-60/AC-80 交付物）。
/// 契约点：预设参数值与架构表逐条一致、Id 稳定（存配置）、FindById 大小写不敏感、
/// 未知 Id 返回 Null / IsKnown False。
/// </summary>
public class StylePresetsTests
{
    [Fact]
    public void All_ContainsFourPresets_InDisplayOrder()
    {
        Assert.Equal(4, StylePresets.All.Count);
        Assert.Equal(
            new[] { "control-classic", "control-highcontrast", "subtitle-light", "cinema-dark" },
            StylePresets.All.Select(p => p.Id));
    }

    [Fact]
    public void ControlClassic_MatchesArchitectureTable()
    {
        var p = StylePresets.ControlClassic;
        Assert.Equal("control-classic", p.Id);
        Assert.Equal(120, p.FontSize);
        Assert.Equal("#FFFFFF", p.Color);
        Assert.Equal("", p.OutlineColor); // 无描边
        Assert.Equal(0, p.OutlineWidth);
        Assert.Equal("center", p.Position);
    }

    [Fact]
    public void ControlHighContrast_MatchesArchitectureTable()
    {
        var p = StylePresets.ControlHighContrast;
        Assert.Equal("control-highcontrast", p.Id);
        Assert.Equal(120, p.FontSize);
        Assert.Equal("#FFFFFF", p.Color);
        Assert.Equal("#000000", p.OutlineColor);
        Assert.Equal(4, p.OutlineWidth);
        Assert.Equal("center", p.Position);
    }

    [Fact]
    public void SubtitleLight_MatchesArchitectureTable()
    {
        var p = StylePresets.SubtitleLight;
        Assert.Equal("subtitle-light", p.Id);
        Assert.Equal(96, p.FontSize);
        Assert.Equal("#FFFFFF", p.Color);
        Assert.Equal("#000000", p.OutlineColor);
        Assert.Equal(3, p.OutlineWidth);
        Assert.Equal("top-center", p.Position);
    }

    [Fact]
    public void CinemaDark_MatchesArchitectureTable()
    {
        var p = StylePresets.CinemaDark;
        Assert.Equal("cinema-dark", p.Id);
        Assert.Equal(110, p.FontSize);
        Assert.Equal("#000000", p.Color);
        Assert.Equal("#FFFFFF", p.OutlineColor);
        Assert.Equal(3, p.OutlineWidth);
        Assert.Equal("center", p.Position);
    }

    [Fact]
    public void FindById_KnownId_ReturnsPreset()
    {
        Assert.Same(StylePresets.ControlClassic, StylePresets.FindById("control-classic"));
    }

    [Fact]
    public void FindById_CaseInsensitive()
    {
        Assert.Same(StylePresets.SubtitleLight, StylePresets.FindById("SUBtitle-LIGHT"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindById_NullOrWhitespace_ReturnsNull(string? id)
    {
        Assert.Null(StylePresets.FindById(id));
    }

    [Fact]
    public void FindById_UnknownId_ReturnsNull()
    {
        Assert.Null(StylePresets.FindById("no-such-preset"));
    }

    [Fact]
    public void IsKnown_KnownId_True_Unknown_False()
    {
        Assert.True(StylePresets.IsKnown("cinema-dark"));
        Assert.False(StylePresets.IsKnown("nope"));
        Assert.False(StylePresets.IsKnown(null));
    }

    [Fact]
    public void All_IdsAreUnique()
    {
        Assert.Equal(StylePresets.All.Count, StylePresets.All.Select(p => p.Id).Distinct().Count());
    }
}