using Object1688.Shared.Text;

namespace Object1688.Tests;

/// <summary>
/// 大字排版引擎测试（架构 §3.2 多行段落模型 / §4.2 wrapStrategy / F-06 富文本行）。
/// 文本测量使用确定性伪测量：宽 = 字符数 × 字号 × 0.5，使折行/缩放可精确断言。
/// </summary>
public class BannerMetricsTests
{
    private const double ScreenWidth = 1000;
    private static readonly double MaxLineWidth = ScreenWidth * 0.9; // 900

    // 伪测量：width = len * fontSize * 0.5
    private static double FakeMeasure(string text, double fontSize) => text.Length * fontSize * 0.5;

    private static BannerRequest CreateRequest(params string[] texts) => new()
    {
        DisplayLines = texts.Select(t => new DisplayLine { Text = t }).ToArray(),
    };

    [Fact]
    public void Calculate_NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BannerMetrics.Calculate(null!, ScreenWidth, FakeMeasure));
    }

    [Fact]
    public void Calculate_NullMeasure_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => BannerMetrics.Calculate(CreateRequest("x"), ScreenWidth, null!));
    }

    [Fact]
    public void Calculate_EmptyDisplayLines_Throws()
    {
        var request = new BannerRequest { DisplayLines = Array.Empty<DisplayLine>() };
        Assert.Throws<ArgumentException>(() => BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure));
    }

    [Fact]
    public void Calculate_SingleShortLine_ReturnsLineMetrics()
    {
        var result = BannerMetrics.Calculate(CreateRequest("Hello"), ScreenWidth, FakeMeasure);

        Assert.Single(result.Lines);
        var line = result.Lines[0];
        Assert.Equal("Hello", line.Text);
        Assert.Equal(96, line.FontSize); // 块级默认字号
        Assert.Equal(5 * 96 * 0.5, line.Width);
        Assert.Equal(96 * (1 + BannerMetrics.LineGapFactor), line.Height);
        Assert.Equal("#FFFFFF", line.Color);
        Assert.Equal(1, result.ShrinkFactor);
        Assert.Equal(BannerMetrics.LineGapFactor, result.LineGap);
        Assert.Equal(line.Width, result.BlockWidth);
        Assert.Equal(line.Height, result.BlockHeight);
    }

    [Fact]
    public void Calculate_AppNamePlaceholder_IsResolved()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "{appName} 已启动" } },
            AppName = "notepad",
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal("notepad 已启动", result.Lines[0].Text);
    }

    [Fact]
    public void Calculate_TimePlaceholder_UsesTimeFormat()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "{time}" } },
            TimeFormat = "HH:mm",
            TriggerTime = new DateTimeOffset(new DateTime(2026, 9, 9, 12, 5, 0, DateTimeKind.Local)),
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal("12:05", result.Lines[0].Text);
    }

    [Fact]
    public void Calculate_LineFontSizeZero_FallsBackToBlockDefault()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A", FontSize = 0 } },
            FontSize = 150,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal(150, result.Lines[0].FontSize);
    }

    [Fact]
    public void Calculate_LineFontSizePositive_OverridesBlock()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A", FontSize = 200 } },
            FontSize = 96,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal(200, result.Lines[0].FontSize);
    }

    [Fact]
    public void Calculate_LineColorEmpty_FallsBackToBlockColor()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A", Color = "" } },
            Color = "#112233",
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal("#112233", result.Lines[0].Color);
    }

    [Fact]
    public void Calculate_LineColor_Positive_OverridesBlock()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A", Color = "#FF0000" } },
            Color = "#FFFFFF",
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal("#FF0000", result.Lines[0].Color);
    }

    [Fact]
    public void Calculate_LineOutlineWidthNegative_FallsBackToBlock()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A", OutlineWidth = -1 } },
            OutlineWidth = 4,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal(4, result.Lines[0].OutlineWidth);
    }

    [Fact]
    public void Calculate_Wrap_LongCjkText_SplitsByChar()
    {
        // 30 字符 × 96 × 0.5 = 1440 > 900 → 按字符硬折（每行最多 18 字符，宽 864 ≤ 900）
        var text = new string('字', 30);
        var result = BannerMetrics.Calculate(CreateRequest(text), ScreenWidth, FakeMeasure);

        Assert.Equal(WrapStrategy.Wrap, result.Lines.Count > 1 ? WrapStrategy.Wrap : WrapStrategy.Manual);
        Assert.True(result.Lines.Count >= 2, $"预期多行，实际 {result.Lines.Count}");
        Assert.All(result.Lines, l => Assert.True(l.Width <= MaxLineWidth + 1, $"行宽 {l.Width} 超限"));
        Assert.Equal(new string('字', 18), result.Lines[0].Text);
        Assert.Equal(new string('字', 12), result.Lines[1].Text);
    }

    [Fact]
    public void Calculate_Wrap_WordBoundary_KeepsWordsTogether()
    {
        // "hello world foo bar baz" = 23 字符 → 1104 > 900
        // 词切分：hello(5) world(5) foo(3) bar(3) baz(3)
        // 行1 "hello"(240) +" world"(528) +" foo"(720) +" bar"(912>900→不入)
        // → 行1="hello world foo"(720) 行2="bar"(144) +" baz"(336)
        var text = "hello world foo bar baz";
        var result = BannerMetrics.Calculate(CreateRequest(text), ScreenWidth, FakeMeasure);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("hello world foo", result.Lines[0].Text);
        Assert.Equal("bar baz", result.Lines[1].Text);
        Assert.All(result.Lines, l => Assert.True(l.Width <= MaxLineWidth + 1));
    }

    [Fact]
    public void Calculate_Wrap_ShortLine_NotWrapped()
    {
        var result = BannerMetrics.Calculate(CreateRequest("Hi"), ScreenWidth, FakeMeasure);
        Assert.Single(result.Lines);
        Assert.Equal("Hi", result.Lines[0].Text);
    }

    [Fact]
    public void Calculate_Manual_Newline_SplitsIntoLines()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "第一行\n第二行" } },
            WrapStrategy = WrapStrategy.Manual,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("第一行", result.Lines[0].Text);
        Assert.Equal("第二行", result.Lines[1].Text);
    }

    [Fact]
    public void Calculate_Manual_LongLine_NoAutoWrap()
    {
        // 手动模式：即使超宽也不折行
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = new string('长', 40) } },
            WrapStrategy = WrapStrategy.Manual,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Single(result.Lines);
        Assert.True(result.Lines[0].Width > MaxLineWidth);
    }

    [Fact]
    public void Calculate_Shrink_LongLine_ScalesFont()
    {
        // "hello world foo bar baz" 23 字符 → 原宽 1104；目标 900 → factor 0.8152
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "hello world foo bar baz" } },
            WrapStrategy = WrapStrategy.Shrink,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);

        Assert.Single(result.Lines);
        var expectedFactor = MaxLineWidth / (23 * 96 * 0.5);
        Assert.Equal(expectedFactor, result.ShrinkFactor, precision: 6);
        Assert.Equal(96 * expectedFactor, result.Lines[0].FontSize, precision: 6);
        Assert.True(result.Lines[0].Width <= MaxLineWidth + 1);
    }

    [Fact]
    public void Calculate_Shrink_ShortLine_NoScale()
    {
        var result = BannerMetrics.Calculate(CreateRequest("Hi"), ScreenWidth, FakeMeasure);
        Assert.Equal(1, result.ShrinkFactor);
        Assert.Equal(96, result.Lines[0].FontSize);
    }

    [Fact]
    public void Calculate_Shrink_ExtremeLong_ClampsAtMinRatio()
    {
        // 40 字符 → 原宽 1920；900/1920 = 0.469 > 0.4，不受最小比例限制影响
        var text = new string('长', 40);
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = text } },
            WrapStrategy = WrapStrategy.Shrink,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal(0.469, result.ShrinkFactor, precision: 3);
    }

    [Fact]
    public void Calculate_Shrink_ClampMinRatioFloor()
    {
        // 200 字符 → 原宽 9600；900/9600 = 0.094 → 钳到 0.4
        var text = new string('长', 200);
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = text } },
            WrapStrategy = WrapStrategy.Shrink,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        Assert.Equal(BannerMetrics.MinShrinkRatio, result.ShrinkFactor);
    }

    [Fact]
    public void Calculate_EmptyLine_KeepsGapHeight()
    {
        // 手动换行产生的空行像素高 = 字号 × LineGapFactor（宽 0）
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A\n\nB" } },
            WrapStrategy = WrapStrategy.Manual,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(string.Empty, result.Lines[1].Text);
        Assert.Equal(0, result.Lines[1].Width);
        Assert.Equal(96 * BannerMetrics.LineGapFactor, result.Lines[1].Height);
    }

    [Fact]
    public void Calculate_BlockHeight_SumsLineHeights()
    {
        var request = new BannerRequest
        {
            DisplayLines = new[] { new DisplayLine { Text = "A" }, new DisplayLine { Text = "B" } },
            WrapStrategy = WrapStrategy.Manual,
        };
        var result = BannerMetrics.Calculate(request, ScreenWidth, FakeMeasure);
        var expected = 2 * 96 * (1 + BannerMetrics.LineGapFactor);
        Assert.Equal(expected, result.BlockHeight, precision: 6);
    }

    // ===== NormalizeHex =====

    [Fact]
    public void NormalizeHex_NullOrEmpty_ReturnsWhite()
    {
        Assert.Equal("#FFFFFF", BannerMetrics.NormalizeHex(null));
        Assert.Equal("#FFFFFF", BannerMetrics.NormalizeHex(""));
        Assert.Equal("#FFFFFF", BannerMetrics.NormalizeHex("   "));
    }

    [Fact]
    public void NormalizeHex_RgbShort_ExpandsToRgb()
    {
        Assert.Equal("#AABBCC", BannerMetrics.NormalizeHex("#ABC"));
    }

    [Fact]
    public void NormalizeHex_RgbFull_Passthrough()
    {
        Assert.Equal("#AABBCC", BannerMetrics.NormalizeHex("#AABBCC"));
    }

    [Fact]
    public void NormalizeHex_Uppercase_Passthrough()
    {
        Assert.Equal("#ABCDEF", BannerMetrics.NormalizeHex("#ABCDEF"));
    }
}