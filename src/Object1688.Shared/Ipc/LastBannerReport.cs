namespace Object1688.Shared.Ipc;

/// <summary>
/// 最近一次大字回看（Main→Monitor，F-30 扩展/AC-92）。
/// Main 每次向 Overlay 下发大字时同步给 Monitor，供性能窗口"回看最近一次实际显示的大字"。
/// </summary>
public sealed class LastBannerReport
{
    /// <summary>渲染后的文字行（displayLines 文本）。</summary>
    public required IReadOnlyList<string> Lines { get; init; }

    /// <summary>命中的规则 ID（欢迎/手动/测试/控制接口触发时为 null）。</summary>
    public string? RuleId { get; init; }

    /// <summary>触发/下发时刻（UTC）。</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
