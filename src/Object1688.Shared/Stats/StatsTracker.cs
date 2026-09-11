using Object1688.Shared.Stats;

namespace Object1688.Shared.Stats;

/// <summary>
/// 触发统计内存累计器（架构 §6.4 / AC-47/AC-63）：规则命中时累加 totalTriggers/perRule/byHour，
/// 定期（合并写入节流，默认 5s）与退出时 flush 到 stats.json（经 <see cref="StatsFile"/> 原子写 + 自愈）。
/// 清除历史可 <see cref="Reset"/> 并落盘空统计。
/// </summary>
public sealed class StatsTracker
{
    private static readonly TimeSpan DefaultFlushInterval = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly string _path;
    private readonly TimeSpan _flushInterval;
    private StatsData _data;
    private DateTimeOffset _lastFlushUtc = DateTimeOffset.MinValue;
    private bool _dirty;

    /// <summary>初始化跟踪器并加载既有统计（损坏自愈由 StatsFile 承担）。</summary>
    /// <param name="path">stats.json 完整路径。</param>
    /// <param name="flushInterval">合并写入节流间隔。</param>
    public StatsTracker(string path, TimeSpan? flushInterval = null)
    {
        _path = path;
        _flushInterval = flushInterval ?? DefaultFlushInterval;
        _data = StatsFile.Load(path).Data;
        _lastFlushUtc = DateTimeOffset.UtcNow; // 节流自加载时刻起算，避免首次 Record 即落盘
    }

    /// <summary>当前累计统计数据（快照）。</summary>
    public StatsData Data
    {
        get
        {
            lock (_gate)
            {
                return Clone(_data);
            }
        }
    }

    /// <summary>
    /// 记录一次触发（规则命中）：累加总量/规则计数/小时分布，并按节流间隔落盘。
    /// </summary>
    /// <param name="ruleId">命中规则标识。</param>
    /// <param name="utcNow">触发时刻（UTC）。</param>
    public void Record(string ruleId, DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            _data.TotalTriggers++;
            _data.UpdatedAt = utcNow;

            if (!_data.PerRule.TryGetValue(ruleId, out var ruleStat))
            {
                ruleStat = new RuleStat();
                _data.PerRule[ruleId] = ruleStat;
            }

            ruleStat.Count++;
            ruleStat.LastTriggerAt = utcNow;

            var hourKey = utcNow.ToLocalTime().Hour.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _data.ByHour.TryGetValue(hourKey, out var hourCount);
            _data.ByHour[hourKey] = hourCount + 1;

            _dirty = true;
            if (utcNow - _lastFlushUtc >= _flushInterval)
            {
                FlushLocked(utcNow);
            }
        }
    }

    /// <summary>清除历史并立即落盘空统计。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _data = new StatsData();
            _dirty = true;
            FlushLocked(DateTimeOffset.UtcNow);
        }
    }

    /// <summary>退出前强制落盘未刷统计（幂等）。</summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_dirty)
            {
                FlushLocked(DateTimeOffset.UtcNow);
            }
        }
    }

    private void FlushLocked(DateTimeOffset now)
    {
        StatsFile.TrySave(_data, _path);
        _lastFlushUtc = now;
        _dirty = false;
    }

    private static StatsData Clone(StatsData source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        TotalTriggers = source.TotalTriggers,
        PerRule = source.PerRule.ToDictionary(kv => kv.Key, kv => new RuleStat
        {
            Count = kv.Value.Count,
            LastTriggerAt = kv.Value.LastTriggerAt,
        }),
        ByHour = new Dictionary<string, long>(source.ByHour),
        UpdatedAt = source.UpdatedAt,
    };
}
