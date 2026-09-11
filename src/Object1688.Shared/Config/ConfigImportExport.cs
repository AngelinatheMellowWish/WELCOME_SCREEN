using System.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 配置导入导出（架构 §5.5，F-22/AC-72）。
/// 导出：完整配置 → log/exports/config-export-yyyyMMdd_HHmmss.json（同秒自动加序号防覆盖）。
/// 导入：读文件 → <see cref="ConfigLoader"/> 的 Parse（宽容：JSON 非法按 CFG-V-1005 语义标记，部分非法规则仍可部分导入）。
/// 备份：导入前由调用方先 exec <see cref="BackupCurrent"/> 落一份还原点（backup-yyyyMMdd_HHmmss.json）。
/// 错误码：导出写失败 IO-E-6001、导入备份失败 CFG-W-1006（不阻断）、读取失败 CFG-E-1001。
/// </summary>
public static class ConfigImportExport
{
    /// <summary>导出/备份默认根目录（相对程序基目录下的 log/exports/）。</summary>
    public static string DefaultExportDirectory => Path.Combine(AppContext.BaseDirectory, "log", "exports");

    /// <summary>导出文件名前缀。</summary>
    public const string ExportPrefix = "config-export-";

    /// <summary>导入前备份文件名前缀。</summary>
    public const string BackupPrefix = "backup-";

    /// <summary>失败时是否保留旧文件（由调用方决定；本类导出的备份操作绝不覆盖用户原 config）。</summary>
    public static string BuildTimestampedName(string prefix)
    {
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        return $"{prefix}{ts}.json";
    }

    /// <summary>
    /// 将完整配置导出为 JSON 文件。
    /// </summary>
    /// <param name="config">待导出配置。</param>
    /// <param name="targetDirectory">目标目录；null = <see cref="DefaultExportDirectory"/>。</param>
    /// <returns>实际写出的完整文件路径。</returns>
    /// <exception cref="IOException">目录不可写/写入失败（消息含 IO-E-6001）。</exception>
    public static string ExportAppConfig(AppConfig config, string? targetDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        var dir = targetDirectory ?? DefaultExportDirectory;
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, BuildTimestampedName(ExportPrefix));
        var seq = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(dir, $"{ExportPrefix}{DateTime.Now:yyyyMMdd_HHmmss}_{seq++}.json");
        }

        try
        {
            File.WriteAllText(path, ConfigSerializer.Serialize(config), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"配置导出写盘失败（{ErrorCodes.IoExportWriteFailed}）：{ex.Message}");
        }

        return path;
    }

    /// <summary>
    /// 将完整配置导出到指定文件路径（用户自选路径，覆盖同名文件）。
    /// </summary>
    /// <param name="config">待导出配置。</param>
    /// <param name="fullPath">目标文件完整路径。</param>
    /// <exception cref="IOException">目录/写入失败（消息含 IO-E-6001）。</exception>
    public static void ExportToFile(AppConfig config, string fullPath)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(fullPath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, ConfigSerializer.Serialize(config), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"配置导出写盘失败（{ErrorCodes.IoExportWriteFailed}）：{ex.Message}");
        }
    }

    /// <summary>
    /// 从文件导入配置（宽容）：JSON 解析失败/规则非法 → 返回可用配置 + 问题清单（非法 JSON 归为 CFG-V-1005）。
    /// </summary>
    /// <param name="path">待导入的 JSON 文件路径。</param>
    /// <returns>解析结果（含问题清单；调用方依据 Issues 决定是否应用与是否回滚）。</returns>
    /// <exception cref="IOException">文件不存在/读取失败（消息含 CFG-E-1001）。</exception>
    public static ConfigLoadResult ImportFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"配置导入读取失败（{ErrorCodes.ConfigReadFailed}）：{path}：{ex.Message}");
        }

        var result = ConfigLoader.Parse(json);

        // 宽容导入语义（架构 §5.5）：格式/字段非法按 CFG-V-1005 归口（区别于加载链路的 CFG-V-1003）
        if (result.Issues.Any(i => i.ErrorCode == ErrorCodes.ConfigValidationFailed))
        {
            var remapped = result.Issues
                .Select(i => i.ErrorCode == ErrorCodes.ConfigValidationFailed
                    ? new ConfigIssue { ErrorCode = ErrorCodes.ConfigImportJsonInvalid, Message = i.Message }
                    : i)
                .ToArray();
            return new ConfigLoadResult { Config = result.Config, Issues = remapped };
        }

        return result;
    }

    /// <summary>
    /// 导入前自动备份当前配置（还原点）。
    /// </summary>
    /// <param name="configPath">当前配置文件路径。</param>
    /// <param name="targetDirectory">备份目录；null = <see cref="DefaultExportDirectory"/>。</param>
    /// <returns>备份文件完整路径。</returns>
    /// <exception cref="IOException">配置不存在/备份写失败（消息含 CFG-W-1006；调用方可 catch 后继续导入）。</exception>
    public static string BackupCurrent(string configPath, string? targetDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        if (!File.Exists(configPath))
        {
            throw new IOException($"当前配置不存在，跳过备份（{ErrorCodes.ConfigImportBackupFailed}）：{configPath}");
        }

        var dir = targetDirectory ?? DefaultExportDirectory;
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, BuildTimestampedName(BackupPrefix));

        try
        {
            File.Copy(configPath, dest, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"配置导入前备份失败（{ErrorCodes.ConfigImportBackupFailed}）：{ex.Message}");
        }

        return dest;
    }
}