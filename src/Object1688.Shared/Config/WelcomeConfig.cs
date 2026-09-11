using Object1688.Shared.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 欢迎大字配置节（架构 §5.7 welcome，F-11：程序每次启动显示）。
/// 对应 config.json welcome 对象。
/// </summary>
public sealed class WelcomeConfig
{
    /// <summary>是否每次启动显示欢迎大字。缺省 true。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>多行段落模型（F-06，每行独立文字/字号/颜色/描边/对齐）。至少一行。</summary>
    public required IReadOnlyList<DisplayLine> DisplayLines { get; init; }

    /// <summary>启动后延时显示秒数。缺省 1。</summary>
    public double DelaySeconds { get; init; } = 1;

    /// <summary>保持显示秒数。缺省 4。</summary>
    public double HoldSeconds { get; init; } = 4;

    /// <summary>换行策略：wrap/shrink/manual。缺省 Wrap。</summary>
    public WrapStrategy WrapStrategy { get; init; } = WrapStrategy.Wrap;
}