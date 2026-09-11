using System.Globalization;
using System.Text.RegularExpressions;
using Object1688.Shared.Config;
using Object1688.Shared.Monitor;

namespace Object1688.Shared.Text;

/// <summary>
/// 大字请求组装器（架构 §4.4 / §5.7 合并语义定稿）。
/// 职责：将「规则（或欢迎配置）+ 全局默认 + 提示音设置」按「规则级字段 → 全局默认」逐项合并为
/// <see cref="BannerRequest"/> 快照，供 Main 下发 TriggerCommand。
/// 合并规则（§5.7）：规则未显式配置的项回退全局默认；描边 <c>outlineColor</c> 空串 /
/// <c>outlineWidth</c> 负值表示「未覆盖，跟随全局」。
/// </summary>
public static partial class BannerAssembler
{
    /// <summary>
    /// 组装规则触发大字（Main 收到 TriggerEvent 后调用）。
    /// </summary>
    /// <param name="rule">命中规则（已由 ConfigLoader 解析；哨兵字段为空串/-1 = 跟随全局）。</param>
    /// <param name="global">全局默认节（§5.7 global）。</param>
    /// <param name="sound">全局提示音节（F-08）；规则级 SoundEnabled 为 null 时跟随。</param>
    /// <param name="appName">占位符 {appName} 替换值（实际命中的进程名/窗口标题）。</param>
    /// <param name="triggerTimeUtc">占位符 {time} 触发时刻（UTC）。</param>
    public static BannerRequest ComposeRule(
        RuleConfig rule, GlobalConfig global, SoundConfig sound,
        string? appName, DateTimeOffset triggerTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(sound);

        var (position, customX, customY) = ResolvePosition(
            string.IsNullOrWhiteSpace(rule.Position) ? global.DefaultPosition : rule.Position);

        return new BannerRequest
        {
            DisplayLines = rule.DisplayLines,
            // 块级默认字号 = 全局 defaultFontSize；行级 FontSize>0 时由渲染层覆盖（BannerMetrics）
            FontSize = global.DefaultFontSize,
            // 块级文字色：无规则/全局独立字段，恒为默认纯白（BannerRequest 缺省）
            Color = "#FFFFFF",
            OutlineColor = string.IsNullOrWhiteSpace(rule.OutlineColor)
                ? global.DefaultOutlineColor
                : rule.OutlineColor,
            OutlineWidth = rule.OutlineWidth < 0 ? global.DefaultOutlineWidth : rule.OutlineWidth,
            OutlineMode = ResolveOutlineMode(rule.OutlineMode, global.OutlineMode),
            // 块级对齐：规则无覆盖字段，缺省 Center（行级 Align 由渲染层逐行生效）
            Align = TextAlignment.Center,
            WrapStrategy = rule.WrapStrategy,
            DelaySeconds = rule.DelaySeconds,
            HoldSeconds = rule.HoldSeconds,
            TargetScreen = string.IsNullOrWhiteSpace(rule.TargetScreen)
                ? global.TargetScreen
                : rule.TargetScreen,
            TimeFormat = global.TimeFormat,
            Position = position,
            CustomX = customX,
            CustomY = customY,
            AppName = appName,
            TriggerTime = triggerTimeUtc,
            PlaySound = rule.SoundEnabled ?? sound.Enabled,
        };
    }

    /// <summary>
    /// 组装欢迎大字（F-11）：全部展示字段取全局默认；时间/换行/保持取自欢迎配置节。
    /// 欢迎大字为启动展示，不播放提示音（F-08 面向规则命中的大字；欢迎配置无声音开关）。
    /// </summary>
    public static BannerRequest ComposeWelcome(WelcomeConfig welcome, GlobalConfig global, SoundConfig sound)
    {
        ArgumentNullException.ThrowIfNull(welcome);
        ArgumentNullException.ThrowIfNull(global);

        var (position, customX, customY) = ResolvePosition(global.DefaultPosition);

        return new BannerRequest
        {
            DisplayLines = welcome.DisplayLines,
            FontSize = global.DefaultFontSize,
            Color = "#FFFFFF",
            OutlineColor = global.DefaultOutlineColor,
            OutlineWidth = global.DefaultOutlineWidth,
            OutlineMode = ResolveOutlineMode(null, global.OutlineMode),
            Align = TextAlignment.Center,
            WrapStrategy = welcome.WrapStrategy,
            DelaySeconds = welcome.DelaySeconds,
            HoldSeconds = welcome.HoldSeconds,
            TargetScreen = global.TargetScreen,
            TimeFormat = global.TimeFormat,
            Position = position,
            CustomX = customX,
            CustomY = customY,
            AppName = null,
            TriggerTime = DateTimeOffset.UtcNow,
            PlaySound = false,
        };
    }

    /// <summary>
    /// 组装手动大字（F-12/AC-29）：独立样式字段（字号/位置/描边/目标屏/对齐/保持/延时/快捷键）取
    /// 手动配置节，未配置项回退全局默认；手动触发为强制路径，不受勿扰/暂停静默（F-25 扩展）。
    /// 提示音沿用全局 sound 开关（F-08：大字浮现播放提示音）。
    /// </summary>
    public static BannerRequest ComposeManual(ManualConfig manual, GlobalConfig global, SoundConfig sound)
    {
        ArgumentNullException.ThrowIfNull(manual);
        ArgumentNullException.ThrowIfNull(global);
        ArgumentNullException.ThrowIfNull(sound);

        var (position, customX, customY) = ResolvePosition(
            string.IsNullOrWhiteSpace(manual.Position) ? global.DefaultPosition : manual.Position);

        return new BannerRequest
        {
            DisplayLines = manual.DisplayLines,
            FontSize = manual.FontSize > 0 ? manual.FontSize : global.DefaultFontSize,
            Color = "#FFFFFF",
            OutlineColor = string.IsNullOrWhiteSpace(manual.OutlineColor)
                ? global.DefaultOutlineColor
                : manual.OutlineColor,
            OutlineWidth = manual.OutlineWidth < 0 ? global.DefaultOutlineWidth : manual.OutlineWidth,
            OutlineMode = ResolveOutlineMode(manual.OutlineMode, global.OutlineMode),
            Align = manual.Align,
            WrapStrategy = manual.WrapStrategy,
            DelaySeconds = manual.DelaySeconds,
            HoldSeconds = manual.HoldSeconds,
            TargetScreen = string.IsNullOrWhiteSpace(manual.TargetScreen)
                ? global.TargetScreen
                : manual.TargetScreen,
            TimeFormat = global.TimeFormat,
            Position = position,
            CustomX = customX,
            CustomY = customY,
            AppName = null,
            TriggerTime = DateTimeOffset.UtcNow,
            PlaySound = sound.Enabled,
        };
    }

    /// <summary>
    /// 解析描边方式（架构 §3.3）：显式值优先，空串/非法回退全局；全局非法回退 <see cref="OutlineMode.Shadow"/>。
    /// </summary>
    private static OutlineMode ResolveOutlineMode(string? specific, string? global)
    {
        if (!string.IsNullOrWhiteSpace(specific) && Enum.TryParse<OutlineMode>(specific.Trim(), ignoreCase: true, out var explicitMode))
        {
            return explicitMode;
        }

        if (!string.IsNullOrWhiteSpace(global) && Enum.TryParse<OutlineMode>(global.Trim(), ignoreCase: true, out var globalMode))
        {
            return globalMode;
        }

        return OutlineMode.Shadow;
    }

    /// <summary>
    /// 解析位置字段：「custom {x,y}」→ Position="custom" + 归一化坐标（0~1，越界钳制）；
    /// 解析失败（如只有 custom 无坐标）→ Position="custom" + 坐标 null（渲染层回退 0.5 居中）；
    /// 其余（center/top-center 等）原样返回。
    /// </summary>
    private static (string Position, double? X, double? Y) ResolvePosition(string position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return ("center", null, null);
        }

        var trimmed = position.Trim();
        if (trimmed.StartsWith("custom", StringComparison.OrdinalIgnoreCase))
        {
            var match = CustomPositionRegex().Match(trimmed);
            if (match.Success
                && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return ("custom", Math.Clamp(x, 0, 1), Math.Clamp(y, 0, 1));
            }

            return ("custom", null, null);
        }

        return (trimmed, null, null);
    }

    [GeneratedRegex(@"^custom\s*\{\s*([0-9]+(?:\.[0-9]+)?)\s*,\s*([0-9]+(?:\.[0-9]+)?)\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex CustomPositionRegex();
}