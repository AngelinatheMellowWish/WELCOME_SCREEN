using System.Text;
using Object1688.Shared;
using Object1688.Shared.Config;
using Object1688.Shared.Text;
using Microsoft.Win32;
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Tests;

/// <summary>
/// ConfigSaver 单测（架构 §5.1/§5.5，M4a 原子写/备份轮转/导入导出/自启）。
/// 契约点：成功保存生成 config.json 且可 Parse 回读一致；备份轮转 bak3→bak1 语义；
/// 写失败保留旧文件并抛含 CFG-E-1010 异常；tmp 无残留；导入导出时间戳防覆盖；
/// 自启注册表（独立测试键，用完自清理）。
/// AppConfig 为 init-only class（非 record）→ 用 CloneWith 辅助重建副本。
/// </summary>
public class ConfigSaverTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "Object1688-SaverTests-" + Guid.NewGuid().ToString("N"));

    public ConfigSaverTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            // 先清除只读属性（写失败测试会设置），再删树
            foreach (var file in Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不影响测试结论
        }
    }

    private string ConfigPath => Path.Combine(_tempDir, "config.json");

    /// <summary>AppConfig 重建副本并覆盖规则/语言节（Model 为 init-only class）。</summary>
    private static AppConfig CloneWith(AppConfig src, string language, params RuleConfig[] rules)
    {
        return new AppConfig
        {
            SchemaVersion = src.SchemaVersion,
            Language = language,
            Global = src.Global,
            Welcome = src.Welcome,
            Manual = src.Manual,
            Rules = rules,
            Dnd = src.Dnd,
            Sound = src.Sound,
            Autostart = src.Autostart,
            FirstRun = src.FirstRun,
        };
    }

    private static AppConfig SampleConfig(string language)
    {
        return CloneWith(ConfigLoader.CreateDefault(), language, new RuleConfig
        {
            RuleId = "r-001",
            MatchType = MatchType.Process,
            MatchValue = "chrome.exe",
            DisplayLines = new[] { new DisplayLine { Text = "浏览器", FontSize = 96 } },
        });
    }

    [Fact]
    public void Save_CreatesConfigJson_ParseableAndPreserved()
    {
        ConfigSaver.Save(ConfigPath, SampleConfig("zh-CN"));

        Assert.True(File.Exists(ConfigPath));
        var result = ConfigLoader.Load(ConfigPath);
        Assert.Empty(result.Issues);
        Assert.Equal("zh-CN", result.Config.Language);
        Assert.Equal("chrome.exe", Assert.Single(result.Config.Rules).MatchValue);
        // 无 BOM
        var bytes = File.ReadAllBytes(ConfigPath);
        Assert.Equal(0x7B, bytes[0]);
        // 无 tmp 残留
        Assert.False(File.Exists(Path.Combine(_tempDir, ConfigSaver.TmpFileName)));
    }

    [Fact]
    public void Save_BackupRotation_ShiftsBak1ToBak3()
    {
        // 预置 3 代历史：bak3=旧3代、bak2=旧2代、bak1=旧1代、config=当前
        File.WriteAllText(ConfigPath, ConfigSerializer.Serialize(SampleConfig("v0")));
        File.WriteAllText(ConfigPath + ".bak1", "{ \"m\": 1 }");
        File.WriteAllText(ConfigPath + ".bak2", "{ \"m\": 2 }");
        File.WriteAllText(ConfigPath + ".bak3", "{ \"m\": 3 }");

        ConfigSaver.Save(ConfigPath, SampleConfig("v1"));

        Assert.True(File.Exists(ConfigPath + ".bak3")); // 旧 bak2 → bak3
        Assert.True(File.Exists(ConfigPath + ".bak2")); // 旧 bak1 → bak2
        Assert.True(File.Exists(ConfigPath + ".bak1")); // 旧 config → bak1
        Assert.Contains("v0", File.ReadAllText(ConfigPath + ".bak1"));
        Assert.Contains("v1", File.ReadAllText(ConfigPath)); // 新内容已落盘
    }

    [Fact]
    public void Save_ReadOnlyDestination_KeepsOldFileAndThrowsCfgE1010()
    {
        // 目标只读 → File.Replace 抛（ReplaceFile 需写目标权限 → UnauthorizedAccessException）
        ConfigSaver.Save(ConfigPath, SampleConfig("old"));
        var oldContent = File.ReadAllText(ConfigPath);
        File.SetAttributes(ConfigPath, FileAttributes.ReadOnly);

        var ex = Assert.Throws<IOException>(() => ConfigSaver.Save(ConfigPath, SampleConfig("new")));

        File.SetAttributes(ConfigPath, FileAttributes.Normal);
        Assert.Contains(ErrorCodes.ConfigWriteFailed, ex.Message);
        Assert.Equal(oldContent, File.ReadAllText(ConfigPath)); // 旧文件保留
        Assert.False(File.Exists(Path.Combine(_tempDir, ConfigSaver.TmpFileName))); // tmp 清理
    }

    [Fact]
    public void Save_InvalidConfigForRoundTrip_ThrowsAndKeepsOld()
    {
        // 序列化回读校验：通配模式缺 * → Parse 产生 CFG-V-1003 → 拒绝写入
        ConfigSaver.Save(ConfigPath, SampleConfig("old"));

        var bad = CloneWith(ConfigLoader.CreateDefault(), "zh-CN", new RuleConfig
        {
            RuleId = "r-bad",
            MatchType = MatchType.Process,
            MatchValue = "chrome",
            MatchMode = MatchMode.Wildcard, // 无 * / ?
            DisplayLines = new[] { new DisplayLine { Text = "x" } },
        });

        var ex = Assert.Throws<IOException>(() => ConfigSaver.Save(ConfigPath, bad));

        Assert.Contains(ErrorCodes.ConfigWriteFailed, ex.Message);
        Assert.False(File.Exists(Path.Combine(_tempDir, ConfigSaver.TmpFileName)));
    }

    // ===== 导入导出（架构 §5.5 / F-22）=====

    [Fact]
    public void ExportAppConfig_WritesTimestampedFile_AndReturnsPath()
    {
        var exportDir = Path.Combine(_tempDir, "exports");
        var path = ConfigImportExport.ExportAppConfig(SampleConfig("en-US"), exportDir);

        Assert.True(File.Exists(path));
        Assert.StartsWith("config-export-", Path.GetFileName(path));
        var result = ConfigLoader.Load(path);
        Assert.Equal("en-US", result.Config.Language);
    }

    [Fact]
    public void ImportFromFile_InvalidJson_ReportsCfgV1005()
    {
        // 非法 JSON → Parse 记 CFG-V-1003 → 导入层重映射为 CFG-V-1005（标注"导入 JSON 非法"）
        var badPath = Path.Combine(_tempDir, "import-bad.json");
        File.WriteAllText(badPath, "{ not valid json !!!", Encoding.UTF8);

        var result = ConfigImportExport.ImportFromFile(badPath);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigImportJsonInvalid);
        Assert.NotNull(result.Config); // 宽容：仍返回可用默认配置
    }

    [Fact]
    public void ImportFromFile_PartiallyInvalidRules_RemapsToCfgV1005()
    {
        // 导入文件中一条规则 matchValue 为空 → Parse 记 CFG-V-1003（跳过该规则）→ 导入层替换为 CFG-V-1005
        var importPath = Path.Combine(_tempDir, "import-partial.json");
        File.WriteAllText(importPath, """
        { "rules": [
            { "ruleId": "r-bad", "matchType": "process", "matchValue": "" },
            { "ruleId": "r-good", "matchType": "process", "matchValue": "chrome.exe", "displayLines": [{ "text": "ok" }] }
          ] }
        """, Encoding.UTF8);

        var result = ConfigImportExport.ImportFromFile(importPath);

        Assert.Contains(result.Issues, i => i.ErrorCode == ErrorCodes.ConfigImportJsonInvalid);
        Assert.Single(result.Config.Rules); // 非法规则丢弃，合法保留（宽容导入）
    }

    [Fact]
    public void ImportFromFile_ValidJson_NoIssues()
    {
        var importPath = Path.Combine(_tempDir, "import-valid.json");
        File.WriteAllText(importPath, """{ "language": "en-US", "rules": [] }""", Encoding.UTF8);

        var result = ConfigImportExport.ImportFromFile(importPath);

        Assert.Empty(result.Issues);
        Assert.Equal("en-US", result.Config.Language);
    }

    [Fact]
    public void BackupCurrent_CopiesWithBackupPrefix()
    {
        ConfigSaver.Save(ConfigPath, SampleConfig("zh-CN"));

        var backupPath = ConfigImportExport.BackupCurrent(ConfigPath, _tempDir);

        Assert.True(File.Exists(backupPath));
        Assert.StartsWith("backup-", Path.GetFileName(backupPath));
        var result = ConfigLoader.Load(backupPath);
        Assert.Equal("zh-CN", result.Config.Language);
    }

    // ===== 自启注册表（架构 §5.5 F-24，独立测试键自清理）=====

    private string TestRunKeyPath { get; } = @"Software\Object1688.Tests\Run-" + Guid.NewGuid().ToString("N");

    private const string TestValueName = "Object1688Test";

    [Fact]
    public void AutostartEnable_IsEnabledTrue_ThenDisableClears()
    {
        try
        {
            Assert.False(AutostartRegistry.IsEnabled(TestRunKeyPath, TestValueName));

            AutostartRegistry.Enable(@"C:\Program Files\Object1688\Object1688.Main.exe", TestRunKeyPath, TestValueName);

            Assert.True(AutostartRegistry.IsEnabled(TestRunKeyPath, TestValueName));

            using var key = Registry.CurrentUser.OpenSubKey(TestRunKeyPath);
            Assert.Equal("\"C:\\Program Files\\Object1688\\Object1688.Main.exe\"", key?.GetValue(TestValueName) as string);

            AutostartRegistry.Disable(TestRunKeyPath, TestValueName);

            Assert.False(AutostartRegistry.IsEnabled(TestRunKeyPath, TestValueName));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(TestRunKeyPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void AutostartDisable_WhenMissing_IsNoOp()
    {
        try
        {
            AutostartRegistry.Disable(TestRunKeyPath, TestValueName); // 不应抛
            Assert.False(AutostartRegistry.IsEnabled(TestRunKeyPath, TestValueName));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(TestRunKeyPath, throwOnMissingSubKey: false);
        }
    }
}