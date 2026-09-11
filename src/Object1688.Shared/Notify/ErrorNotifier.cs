namespace Object1688.Shared.Notify;

/// <summary>
/// 错误提示去重聚合器（需求书 F-42 / AC-84）：同一错误码在窗口（默认 30s）内仅首次弹一次气泡，
/// 窗口内重复出现被抑制并累计；累计达聚合阈值（默认 5 次）时输出聚合提示（"已发生 N 次"）并重置窗口，
/// 避免错误风暴刷屏。核心为纯逻辑，不依赖 UI，便于单测。
/// </summary>
public sealed class ErrorNotifier
{
    /// <summary>默认去重窗口（30s）。</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(30);

    /// <summary>默认聚合阈值（N 次致命聚合为一次提示）。</summary>
    public const int DefaultAggregateThreshold = 5;

    private readonly TimeSpan _window;
    private readonly int _aggregateThreshold;
    private readonly object _gate = new();
    private readonly Dictionary<string, (DateTimeOffset FirstSeen, int Count)> _state = new();

    /// <summary>初始化去重聚合器。</summary>
    /// <param name="window">去重窗口时长。</param>
    /// <param name="aggregateThreshold">聚合阈值（同码窗口内累计达该值输出聚合提示）。</param>
    public ErrorNotifier(
        TimeSpan? window = null,
        int aggregateThreshold = DefaultAggregateThreshold)
    {
        _window = window ?? DefaultWindow;
        _aggregateThreshold = aggregateThreshold > 1 ? aggregateThreshold : 2;
    }

    /// <summary>
    /// 依据错误码与当前时刻决定提示动作。
    /// </summary>
    /// <param name="errorCode">统一错误码（null/空 视作普通提示，不参与去重，恒返回 Show）。</param>
    /// <param name="utcNow">当前 UTC 时刻（测试可注入）。</param>
    /// <returns>去重后的提示动作。</returns>
    public NotificationAction Decide(string? errorCode, DateTimeOffset utcNow)
    {
        if (string.IsNullOrEmpty(errorCode))
        {
            return new NotificationAction(NotificationKind.Show, 1);
        }

        lock (_gate)
        {
            if (!_state.TryGetValue(errorCode, out var entry) || utcNow - entry.FirstSeen > _window)
            {
                _state[errorCode] = (utcNow, 1);
                return new NotificationAction(NotificationKind.Show, 1);
            }

            var nextCount = entry.Count + 1;
            if (nextCount >= _aggregateThreshold)
            {
                // 达聚合阈值：输出聚合提示并清空条目（下一周期从 Show 重新开始）
                _state.Remove(errorCode);
                return new NotificationAction(NotificationKind.Aggregate, nextCount);
            }

            _state[errorCode] = (entry.FirstSeen, nextCount);
            return new NotificationAction(NotificationKind.Suppressed, nextCount);
        }
    }

    /// <summary>清空去重状态（如用户主动清除历史）。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _state.Clear();
        }
    }
}

/// <summary>错误提示动作（AC-84 判定结果）。</summary>
public readonly record struct NotificationAction(NotificationKind Kind, int CumulativeCount);

/// <summary>错误提示动作种类。</summary>
public enum NotificationKind
{
    /// <summary>弹气泡（含错误码）——窗口内首次或新周期首见。</summary>
    Show,

    /// <summary>抑制（窗口内重复，仅累计计数）。</summary>
    Suppressed,

    /// <summary>聚合提示（"已发生 N 次"，点击查看日志）——达聚合阈值时。</summary>
    Aggregate,
}