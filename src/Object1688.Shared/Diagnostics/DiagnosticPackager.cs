using System.IO.Compression;
using System.Text;

namespace Object1688.Shared.Diagnostics;

/// <summary>诊断包生成结果。</summary>
/// <param name="Path">生成的 zip 完整路径。</param>
/// <param name="Partial">是否部分失败（有缺失/写入失败项，对应 IO-W-6004）。</param>
/// <param name="Warnings">问题清单（缺失文件/写入失败）。</param>
public sealed record DiagnosticPackResult(string Path, bool Partial, IReadOnlyList<string> Warnings);

/// <summary>
/// 一键诊断包（架构 F-30 扩展 / AC-83）。
/// 将日志（log/*.log）、配置（config.json）、统计（stats.json）与环境信息打包为 zip，
/// 便于用户反馈问题；单项缺失/失败不阻断（返回 Partial=true，对应 IO-W-6004）。
/// </summary>
public static class DiagnosticPackager
{
    /// <summary>默认输出目录（程序基目录 log/exports/）。</summary>
    public static string DefaultOutputDirectory => System.IO.Path.Combine(AppContext.BaseDirectory, "log", "exports");

    /// <summary>
    /// 生成诊断包。
    /// </summary>
    /// <param name="outputDirectory">输出目录（不存在自动创建）。</param>
    /// <param name="logDirectory">日志目录（收集其中 *.log）。</param>
    /// <param name="configPath">配置文件路径（可缺失）。</param>
    /// <param name="statsPath">统计文件路径（可缺失）。</param>
    /// <param name="appVersion">程序版本号（写入环境信息）。</param>
    /// <returns>诊断包结果（Partial 表示有缺失/失败项）。</returns>
    public static DiagnosticPackResult Create(
        string outputDirectory,
        string logDirectory,
        string configPath,
        string statsPath,
        string appVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var path = System.IO.Path.Combine(outputDirectory, $"diagnostics-{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        var seq = 1;
        while (File.Exists(path))
        {
            path = System.IO.Path.Combine(outputDirectory, $"diagnostics-{DateTime.Now:yyyyMMdd_HHmmss}_{seq++}.zip");
        }

        var warnings = new List<string>();
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            AddText(zip, "environment.txt", BuildEnvironmentText(appVersion), warnings);
            AddFile(zip, configPath, "config.json", warnings);
            AddFile(zip, statsPath, "stats.json", warnings);

            if (Directory.Exists(logDirectory))
            {
                foreach (var log in Directory.EnumerateFiles(logDirectory, "*.log"))
                {
                    AddFile(zip, log, "logs/" + System.IO.Path.GetFileName(log), warnings);
                }
            }
            else
            {
                warnings.Add($"日志目录不存在：{logDirectory}");
            }
        }

        return new DiagnosticPackResult(path, warnings.Count > 0, warnings);
    }

    private static string BuildEnvironmentText(string appVersion) =>
        $"Object1688 诊断包{Environment.NewLine}" +
        $"生成时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
        $"程序版本：{appVersion}{Environment.NewLine}" +
        $"操作系统：{Environment.OSVersion}{Environment.NewLine}" +
        $"系统架构：{System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}{Environment.NewLine}" +
        $".NET：{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}{Environment.NewLine}" +
        $"机器名：{Environment.MachineName}{Environment.NewLine}";

    private static void AddText(ZipArchive zip, string entryName, string text, List<string> warnings)
    {
        try
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{entryName}：{ex.Message}");
        }
    }

    private static void AddFile(ZipArchive zip, string sourcePath, string entryName, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            warnings.Add($"缺失：{sourcePath}");
            return;
        }

        try
        {
            // 日志文件可能被 Logging 进程以写句柄持有：以 ReadWrite|Delete 共享打开后拷贝进 zip
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            source.CopyTo(entryStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"{entryName}：{ex.Message}");
        }
    }
}
