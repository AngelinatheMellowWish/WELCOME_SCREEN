using Microsoft.Win32;

namespace Object1688.Shared.Config;

/// <summary>
/// 开机自启注册表控制（架构 §5.5 F-24，应用根节点 AppConfig.Autostart）。
/// 写/读/删 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 的 Object1688 项（值 = 引号包裹的启动 exe 全路径）。
/// 失败抛含 PRC-W-2005 的 IOException（写入 Run 键通常无需管理员权限；异常场景：策略限制/权限异常）。
/// 测试可注入独立注册表键路径与值名，避免污染真实 Run 键。
/// </summary>
public static class AutostartRegistry
{
    /// <summary>真实 Run 键路径（HKCU 根）。</summary>
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>真实值名。</summary>
    public const string DefaultValueName = "Object1688";

    private static string ResolveExecutablePath(string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return Path.GetFullPath(executablePath);
        }

        var procPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(procPath))
        {
            return Path.GetFullPath(procPath);
        }

        // 兜底：预期部署布局下 Main 与 ConfigUI 同目录的主程序名
        var fallback = Path.Combine(AppContext.BaseDirectory, "Object1688.Main.exe");
        return fallback;
    }

    /// <summary>
    /// 查询自启是否已注册（值存在且非空白）。
    /// </summary>
    /// <param name="runKeyPath">注册表键路径（默认 <see cref="DefaultRunKeyPath"/>）。</param>
    /// <param name="valueName">值名（默认 <see cref="DefaultValueName"/>）。</param>
    /// <returns>true = 已注册。</returns>
    public static bool IsEnabled(string? runKeyPath = null, string? valueName = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath ?? DefaultRunKeyPath, writable: false);
            var value = key?.GetValue(valueName ?? DefaultValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            throw new IOException($"自启状态查询失败（{ErrorCodes.AutostartWriteFailed}）：{ex.Message}");
        }
    }

    /// <summary>
    /// 写入自启项（已存在则覆盖为当前 exe 路径）。
    /// </summary>
    /// <param name="executablePath">自启 exe 全路径；null = 当前进程路径。</param>
    /// <param name="runKeyPath">注册表键路径（默认 <see cref="DefaultRunKeyPath"/>）。</param>
    /// <param name="valueName">值名（默认 <see cref="DefaultValueName"/>）。</param>
    /// <exception cref="IOException">写失败（消息含 PRC-W-2005）。</exception>
    public static void Enable(string? executablePath = null, string? runKeyPath = null, string? valueName = null)
    {
        var exe = ResolveExecutablePath(executablePath);
        var quoteValue = $"\"{exe}\"";

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(runKeyPath ?? DefaultRunKeyPath, writable: true);
            key?.SetValue(valueName ?? DefaultValueName, quoteValue, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            throw new IOException($"自启注册表写入失败（{ErrorCodes.AutostartWriteFailed}）：{exe}：{ex.Message}");
        }
    }

    /// <summary>
    /// 删除自启项（未注册时静默成功）。
    /// </summary>
    /// <param name="runKeyPath">注册表键路径（默认 <see cref="DefaultRunKeyPath"/>）。</param>
    /// <param name="valueName">值名（默认 <see cref="DefaultValueName"/>）。</param>
    /// <exception cref="IOException">删除失败（消息含 PRC-W-2005）。</exception>
    public static void Disable(string? runKeyPath = null, string? valueName = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath ?? DefaultRunKeyPath, writable: true);
            key?.DeleteValue(valueName ?? DefaultValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            throw new IOException($"自启注册表删除失败（{ErrorCodes.AutostartWriteFailed}）：{ex.Message}");
        }
    }

    /// <summary>
    /// 读取自启项指向的 exe 路径（去除外层引号）；未注册/空白返回 null。
    /// </summary>
    /// <param name="runKeyPath">注册表键路径（默认 <see cref="DefaultRunKeyPath"/>）。</param>
    /// <param name="valueName">值名（默认 <see cref="DefaultValueName"/>）。</param>
    public static string? GetRegisteredPath(string? runKeyPath = null, string? valueName = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKeyPath ?? DefaultRunKeyPath, writable: false);
            var raw = key?.GetValue(valueName ?? DefaultValueName) as string;
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().Trim('"');
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            throw new IOException($"自启状态查询失败（{ErrorCodes.AutostartWriteFailed}）：{ex.Message}");
        }
    }

    /// <summary>
    /// 校验自启项是否指向当前 exe（AC-53）：未注册 / 指向旧路径 / 与当前 exe 不一致 → false。
    /// 仅读校验，**不自动改写注册项**（防止指向被替换的恶意文件）。
    /// </summary>
    /// <param name="currentExePath">当前主程序 exe 全路径。</param>
    /// <param name="runKeyPath">注册表键路径（默认 <see cref="DefaultRunKeyPath"/>）。</param>
    /// <param name="valueName">值名（默认 <see cref="DefaultValueName"/>）。</param>
    public static bool IsRegisteredPathValid(string currentExePath, string? runKeyPath = null, string? valueName = null)
    {
        if (string.IsNullOrWhiteSpace(currentExePath))
        {
            return false;
        }

        var registered = GetRegisteredPath(runKeyPath, valueName);
        if (registered is null)
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(registered).TrimEnd('\\'),
                Path.GetFullPath(currentExePath).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}