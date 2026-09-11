using System.Reflection;
using System.Text.RegularExpressions;
using Object1688.Shared;

namespace Object1688.Tests;

/// <summary>
/// ErrorCodes 常量集一致性测试（docs/error_codes[v1.0.0].md 表 49 条）。
/// 硬性规则：不删除、字符串与文档一致、新增必须先登记文档。
/// </summary>
public class ErrorCodesTests
{
    private const int ExpectedCodeCount = 49;

    /// <summary>前缀合法集合（与文档分区一一对应）。</summary>
    private static readonly string[] ValidPrefixes =
    {
        "CFG", "PRC", "OVL", "LOG", "CRS", "MON", "IO", "IPC", "GEN",
    };

    private static readonly Regex CodeFormat = new(
        @"^(CFG|PRC|OVL|LOG|CRS|MON|IO|IPC|GEN)-[EIWV]-\d{4}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void AllCodes_Count_Is49()
    {
        Assert.Equal(ExpectedCodeCount, ErrorCodes.AllCodes.Count);
    }

    [Fact]
    public void AllCodes_HaveNoDuplicates()
    {
        var duplicates = ErrorCodes.AllCodes.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllCodes_MatchDocumentedFormat()
    {
        foreach (var code in ErrorCodes.AllCodes)
        {
            Assert.Matches(CodeFormat, code);
        }
    }

    [Theory]
    [InlineData("CFG", 1000, 1999)]
    [InlineData("PRC", 2000, 2999)]
    [InlineData("OVL", 3000, 3999)]
    [InlineData("LOG", 4000, 4999)]
    [InlineData("CRS", 4000, 4999)]
    [InlineData("MON", 5000, 5999)]
    [InlineData("IO", 6000, 6999)]
    [InlineData("IPC", 7000, 7999)]
    [InlineData("GEN", 9000, 9999)]
    public void AllCodes_BelongToExpectedRanges(string prefix, int min, int max)
    {
        var codes = ErrorCodes.AllCodes.Where(c => c.StartsWith(prefix + "-", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(codes);
        foreach (var code in codes)
        {
            var number = int.Parse(code.Split('-')[2], System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(number, min, max);
        }
    }

    /// <summary>
    /// 反射校验：每个 public const string 字段（常量）都必须登记在 AllCodes 中，
    /// 且 AllCodes 不得包含未定义字段——防止"新码漏登记"与"AllCodes 手抖多写"两类错误。
    /// </summary>
    [Fact]
    public void Constants_And_AllCodes_AreIdenticalSets()
    {
        var constValues = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, FieldType.FullName: "System.String" })
            .Select(f => (string)f.GetValue(null)!)
            .ToArray();

        Assert.Equal(constValues.OrderBy(c => c), ErrorCodes.AllCodes.OrderBy(c => c));
    }

    [Fact]
    public void Severity_Prefix_MatchesErrorCodeCategory()
    {
        // 严重级：E=错误 / W=警告 / I=信息 / V=校验失败（CFG-V-xxxx 系列）
        foreach (var code in ErrorCodes.AllCodes)
        {
            var severity = code.Split('-')[1];
            Assert.Contains(severity, new[] { "E", "W", "I", "V" });
        }
    }

    [Fact]
    public void Prefixes_AreCanonical()
    {
        var usedPrefixes = ErrorCodes.AllCodes.Select(c => c.Split('-')[0]).Distinct().OrderBy(p => p).ToArray();
        Assert.Equal(ValidPrefixes.OrderBy(p => p), usedPrefixes);
    }
}