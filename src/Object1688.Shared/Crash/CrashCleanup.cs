namespace Object1688.Shared.Crash;

/// <summary>
/// crash 目录上限清理器（架构 §7.2 / AC-52）：保留最近 <see cref="DefaultMaxFileCount"/> 个
/// 或总大小 ≤ <see cref="DefaultMaxTotalBytes"/>（先到为准），超出删除最旧的转储文件。
/// 清理失败不抛异常——返回失败供调用方记录 CRS-W-4005（仅记日志，不阻断）。
/// </summary>
public static class CrashCleanup
{
    /// <summary>默认保留转储文件数量上限（10 个）。</summary>
    public const int DefaultMaxFileCount = 10;

    /// <summary>默认转储总大小上限（200MB）。</summary>
    public const long DefaultMaxTotalBytes = 200L * 1024 * 1024;

    /// <summary>
    /// 对目录执行上限清理。
    /// </summary>
    /// <param name="directory">崩溃转储目录。</param>
    /// <param name="maxFileCount">保留数量上限。</param>
    /// <param name="maxTotalBytes">保留总大小上限。</param>
    /// <returns>清理是否成功（false = 发生 IO 错误，调用方应记录 CRS-W-4005）。</returns>
    public static bool TryCleanup(
        string directory,
        int maxFileCount = DefaultMaxFileCount,
        long maxTotalBytes = DefaultMaxTotalBytes)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return true; // 无目录即无需清理
            }

            var dumps = Directory.GetFiles(directory, "crash_*.dmp")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            var deleteTargets = new List<FileInfo>();
            long total = 0;
            var kept = 0;
            foreach (var dump in dumps)
            {
                if (kept < maxFileCount && total + dump.Length <= maxTotalBytes)
                {
                    kept++;
                    total += dump.Length;
                    continue;
                }

                deleteTargets.Add(dump);
            }

            foreach (var stale in deleteTargets)
            {
                File.Delete(stale.FullName);
            }

            return true;
        }
        catch (IOException)
        {
            return false; // 删除失败：CRS-W-4005 语义由调用方承载
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}