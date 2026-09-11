using Object1688.Shared.Config;
// System.IO.MatchType 与配置 MatchType 同名（SDK 隐式 using），按既有别名模式消歧
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Shared.Monitor;

/// <summary>
/// 触发命中上报（架构 §4.4，Overlay→Main，载荷）。
/// 由 Overlay 监测循环在规则命中时组装并发送 <c>TriggerEvent</c> 消息；
/// Main 收到后按 <see cref="RuleId"/> 查规则 → 组装 <see cref="Text.BannerRequest"/> → TriggerCommand 回推。
/// <see cref="MatchedText"/> 为实际命中的进程名/窗口标题，供 {appName} 占位符替换（F-04）。
/// </summary>
public sealed class TriggerEventReport
{
    /// <summary>命中规则 ID（Main 配置中对应 RuleConfig.RuleId）。</summary>
    public required string RuleId { get; init; }

    /// <summary>命中的目标类型（进程名 / 窗口标题）。</summary>
    public required MatchType MatchType { get; init; }

    /// <summary>规则匹配值（RuleConfig.MatchValue，用于日志可观测性）。</summary>
    public required string MatchValue { get; init; }

    /// <summary>实际命中的文本（进程名或窗口标题；供 {appName} 占位符，F-04）。</summary>
    public required string MatchedText { get; init; }

    /// <summary>命中进程 PID（日志/去重可观测性）。</summary>
    public required int ProcessId { get; init; }

    /// <summary>命中目标是否处于全屏状态（AC-05）。</summary>
    public bool IsFullscreen { get; init; }

    /// <summary>是否进程消失重现补触（minInterval 豁免，§4.4/AC-46）。</summary>
    public bool IsReappearTrigger { get; init; }

    /// <summary>触发时点（UTC，供 {time} 占位符/统计）。</summary>
    public DateTimeOffset TriggeredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}