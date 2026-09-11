namespace Object1688.Shared.Config;

/// <summary>
/// 自定义提示音资源校验（需求书 F-08 / AC-72 / CFG-V-1007）：
/// 格式（扩展名 wav/mp3 白名单）+ 大小上限 + 路径安全（相对路径防目录穿越）。
/// 文件不存在亦视为校验失败（无法保证大小/可播放性），统一归 CFG-V-1007。
/// </summary>
public static class SoundAssetValidator
{
    /// <summary>允许的音频扩展名白名单（小写，不含点）。</summary>
    public static readonly IReadOnlyList<string> AllowedExtensions = new[] { "wav", "mp3" };

    /// <summary>默认单文件大小上限（10MB）。</summary>
    public const long DefaultMaxBytes = 10L * 1024 * 1024;

    /// <summary>
    /// 校验自定义提示音配置。
    /// </summary>
    /// <param name="customPath">sound.customPath 配置值。</param>
    /// <param name="baseDirectory">基准目录（相对路径解析根，通常为程序/配置目录）。</param>
    /// <param name="maxBytes">大小上限（字节）。</param>
    /// <returns>问题清单（空 = 合法）；一律归 CFG-V-1007。</returns>
    public static IReadOnlyList<ConfigIssue> Validate(string? customPath, string baseDirectory, long maxBytes = DefaultMaxBytes)
    {
        var issues = new List<ConfigIssue>();
        if (string.IsNullOrWhiteSpace(customPath))
        {
            issues.Add(CFG_V_1007("sound.customPath 为空：Source=custom 时必须指定音频文件路径"));
            return issues;
        }

        var ext = Path.GetExtension(customPath).TrimStart('.').ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            issues.Add(CFG_V_1007($"sound.customPath 扩展名非法（仅支持 {string.Join("/", AllowedExtensions)}）：{customPath}"));
            return issues; // 扩展名非法即终判，避免继续解析路径
        }

        string fullPath;
        try
        {
            if (Path.IsPathRooted(customPath))
            {
                fullPath = Path.GetFullPath(customPath); // 绝对路径：允许任意可访问位置
            }
            else
            {
                // 相对路径防穿越：解析后必须落在基准目录内（不得 .. 逃逸）
                fullPath = Path.GetFullPath(Path.Combine(baseDirectory, customPath));
                var root = Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(CFG_V_1007($"sound.customPath 路径越界（不得逃出资源目录）：{customPath}"));
                    return issues;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            issues.Add(CFG_V_1007($"sound.customPath 路径非法：{customPath}"));
            return issues;
        }

        if (!File.Exists(fullPath))
        {
            issues.Add(CFG_V_1007($"sound.customPath 文件不存在：{customPath}"));
            return issues;
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (info.Length > maxBytes)
            {
                issues.Add(CFG_V_1007($"sound.customPath 超出大小上限（{maxBytes / 1024 / 1024}MB）：{customPath}"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(CFG_V_1007($"sound.customPath 无法读取文件大小：{customPath}"));
        }

        return issues;
    }

    private static ConfigIssue CFG_V_1007(string message) => new()
    {
        ErrorCode = ErrorCodes.ConfigSoundInvalid,
        Message = message,
    };
}