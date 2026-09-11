using System.Text;
using System.Threading;

namespace Object1688.Shared.Config;

/// <summary>
/// 配置保存器（架构 §5.1/§5.6，F-21/AC-46）。
/// 原子写流程：跨进程命名互斥 → 备份轮转（config.json → bak1 → bak2 → bak3）→ 写 config.json.tmp（UTF-8 无 BOM）
/// → 临时文件回读校验（<see cref="ConfigLoader"/> 的 Parse 重载，防"序列化即损坏"）→ <see cref="File"/> 的 Replace 原子替换。
/// 任一步写失败：保留旧文件、删除残留 tmp、抛包含 CFG-E-1010 的 IOException（调用方可据此决定是否回滚 UI 状态）。
/// </summary>
public static class ConfigSaver
{
    /// <summary>跨进程写锁名（进程内唯一：Main 保存 / ConfigUI 保存 / 导入恢复互斥）。</summary>
    public const string WriteMutexName = "Object1688.ConfigWrite";

    /// <summary>临时文件名（与最终 config.json 同目录，保证同卷原子替换）。</summary>
    public const string TmpFileName = "config.json.tmp";

    /// <summary>获取写锁超时（秒）。</summary>
    private const int LockTimeoutSeconds = 5;

    /// <summary>
    /// 原子保存配置到指定路径。
    /// </summary>
    /// <param name="configPath">最终配置路径（如 %APPDATA%\Object1688\config.json）。</param>
    /// <param name="config">待保存的完整配置。</param>
    /// <exception cref="IOException">持有锁超时 / 写临时文件失败 / 回读校验失败 / 原子替换失败（消息含 CFG-E-1010；旧文件保留）。</exception>
    public static void Save(string configPath, AppConfig config)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tmpPath = Path.Combine(directory ?? string.Empty, TmpFileName);

        // 跨进程写锁：ConfigUI 与 Main 可能并发保存（导入/恢复默认场景），串行化防 torn write
        using var mutex = new Mutex(initiallyOwned: false, name: WriteMutexName);
        bool acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(LockTimeoutSeconds));
            }
            catch (AbandonedMutexException)
            {
                // 持有者崩溃退出：互斥对象仍可用，视同成功获取
                acquired = true;
            }

            if (!acquired)
            {
                throw new IOException($"配置写入锁（{WriteMutexName}）获取超时（{LockTimeoutSeconds}s），可能有其他进程正在写配置（{ErrorCodes.ConfigWriteFailed}）");
            }

            // 备份轮转：bak2→bak3、bak1→bak2、config.json→bak1（保留最近 3 份历史，架构 F-21）
            RotateBackups(configPath);

            // 1) 写临时文件（UTF-8 无 BOM，与 config.default.json 现状一致）
            var json = ConfigSerializer.Serialize(config);
            File.WriteAllText(tmpPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // 2) 回读校验：序列化结果必须能被加载器解析回强类型（防"序列化即损坏"）
            try
            {
                var jsonCheck = File.ReadAllText(tmpPath, Encoding.UTF8);
                var parsed = ConfigLoader.Parse(jsonCheck);
                if (parsed.Issues.Any(i => i.ErrorCode == ErrorCodes.ConfigValidationFailed))
                {
                    var first = parsed.Issues.First(i => i.ErrorCode == ErrorCodes.ConfigValidationFailed);
                    throw new InvalidOperationException($"序列化回读校验未通过：{first.Message}");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                TryDeleteTmp(tmpPath);
                throw new IOException($"配置序列化校验失败，已放弃写入并保留旧文件（{ErrorCodes.ConfigWriteFailed}）：{ex.Message}");
            }

            // 3) 原子替换：File.Replace（目标存在，同卷原子）→ 目标不存在则 Move（首次保存）
            try
            {
                if (File.Exists(configPath))
                {
                    File.Replace(tmpPath, configPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tmpPath, configPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDeleteTmp(tmpPath);
                throw new IOException($"配置写盘失败，已保留旧文件（{ErrorCodes.ConfigWriteFailed}）：{ex.Message}");
            }
        }
        finally
        {
            if (acquired)
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // 未持有锁（异常路径已提前返回），忽略
                }
            }
        }
    }

    private static void RotateBackups(string configPath)
    {
        var bak1 = configPath + ".bak1";
        var bak2 = configPath + ".bak2";
        var bak3 = configPath + ".bak3";

        if (File.Exists(bak2))
        {
            File.Copy(bak2, bak3, overwrite: true);
        }

        if (File.Exists(bak1))
        {
            File.Copy(bak1, bak2, overwrite: true);
        }

        if (File.Exists(configPath))
        {
            File.Copy(configPath, bak1, overwrite: true);
        }
    }

    private static void TryDeleteTmp(string tmpPath)
    {
        try
        {
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 清理失败不掩盖主异常
        }
    }
}