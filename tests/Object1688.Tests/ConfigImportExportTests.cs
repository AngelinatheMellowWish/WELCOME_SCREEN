using System.Text;
using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Text;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// 配置导入导出测试（F-22/AC-21/AC-72）。
/// 契约点：导出（目录/精确路径）→ 可解析回读；导入前备份；导入往返；非法 JSON 归口 CFG-V-1005 且返回可用配置。
/// </summary>
public class ConfigImportExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "o1688-importexport-" + Guid.NewGuid().ToString("N"));

    public ConfigImportExportTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // 忽略清理失败
        }
    }

    private static AppConfig SampleConfig()
    {
        var c = ConfigLoader.CreateDefault();
        return new AppConfig
        {
            SchemaVersion = c.SchemaVersion,
            Language = "zh-CN",
            Global = c.Global,
            Welcome = c.Welcome,
            Manual = c.Manual,
            Rules = new[]
            {
                new RuleConfig
                {
                    RuleId = "r-1",
                    MatchType = MatchType.Process,
                    MatchValue = "chrome.exe",
                    DisplayLines = new[] { new DisplayLine { Text = "浏览器" } },
                },
            },
            Dnd = c.Dnd,
            Sound = c.Sound,
            Autostart = c.Autostart,
            FirstRun = c.FirstRun,
        };
    }

    [Fact]
    public void ExportAppConfig_WritesParseableFileWithPrefix()
    {
        var path = ConfigImportExport.ExportAppConfig(SampleConfig(), _dir);

        Assert.True(File.Exists(path));
        Assert.StartsWith(ConfigImportExport.ExportPrefix, Path.GetFileName(path));
        var reparsed = ConfigLoader.Parse(File.ReadAllText(path, Encoding.UTF8));
        Assert.Equal("chrome.exe", Assert.Single(reparsed.Config.Rules).MatchValue);
    }

    [Fact]
    public void ExportToFile_WritesExactPath()
    {
        var target = Path.Combine(_dir, "my-config.json");
        ConfigImportExport.ExportToFile(SampleConfig(), target);

        Assert.True(File.Exists(target));
        Assert.Equal("zh-CN", ConfigLoader.Parse(File.ReadAllText(target, Encoding.UTF8)).Config.Language);
    }

    [Fact]
    public void BackupCurrent_CopiesExistingConfig_AndThrowsWhenMissing()
    {
        var configPath = Path.Combine(_dir, "config.json");
        ConfigImportExport.ExportToFile(SampleConfig(), configPath);

        var backup = ConfigImportExport.BackupCurrent(configPath, _dir);
        Assert.True(File.Exists(backup));
        Assert.StartsWith(ConfigImportExport.BackupPrefix, Path.GetFileName(backup));

        Assert.Throws<IOException>(() => ConfigImportExport.BackupCurrent(Path.Combine(_dir, "missing.json"), _dir));
    }

    [Fact]
    public void ImportFromFile_ExportedConfig_RoundTripsWithoutIssues()
    {
        var path = ConfigImportExport.ExportAppConfig(SampleConfig(), _dir);

        var result = ConfigImportExport.ImportFromFile(path);

        Assert.Empty(result.Issues);
        Assert.Equal("chrome.exe", Assert.Single(result.Config.Rules).MatchValue);
    }

    [Fact]
    public void ImportFromFile_InvalidJson_ReportsCfgV1005()
    {
        var path = Path.Combine(_dir, "broken.json");
        File.WriteAllText(path, "{ this is not json", new UTF8Encoding(false));

        var result = ConfigImportExport.ImportFromFile(path);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigImportJsonInvalid);
    }
}
