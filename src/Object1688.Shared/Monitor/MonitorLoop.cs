using Object1688.Shared.Config;

namespace Object1688.Shared.Monitor;

/// <summary>
/// 监测循环（架构 §4.1/§4.5，AC-45，M3c）。
/// 职责：按 <c>global.monitorPollIntervalMs</c> 周期采集快照（进程 + 窗口）→ 构建
/// <see cref="MatchTarget"/> 列表 → 交由 <see cref="MonitorEngine"/> 评估 → 分发
/// 触发/抑制/异常回调。
/// 配置热更新：每轮从 <see cref="Func{AppConfig}"/> 取值，引用变化（ConfigChanged 新实例）时重建引擎。
/// 自身进程排除（AC-45）：按「可执行文件与自程序同目录」判定（与 Main 握手校验 ValidatePeer
/// 同法），每轮动态刷新 PID 集合（子进程重启 PID 变化自愈）；引擎亦接收同一集合引用双重过滤。
/// 线程模型：单实例单线程驱动（PollOnce 串行），无内部锁；由 Overlay 侧 RunAsync 周期调用。
/// </summary>
public sealed class MonitorLoop
{
    private readonly Func<AppConfig> _configProvider;
    private readonly IMonitorSnapshotProvider _snapshotProvider;
    private readonly string _selfExecutableDirectory;
    private readonly Action<MonitorTrigger> _onTrigger;
    private readonly Action<MonitorSuppression>? _onSuppression;
    private readonly Action<string>? _onError;
    private readonly Action<bool>? _onFullscreenChanged;

    private readonly HashSet<int> _selfPids = []; // 动态刷新；MonitorEngine 持有同一引用（实时生效）
    private MonitorEngine? _engine;
    private AppConfig? _appliedConfig;
    private long _pausedUntilTicks;
    private bool? _lastFullscreen;

    /// <summary>
    /// 暂停监测至指定时长之后（AC-41：睡眠唤醒后延迟一轮，抑制进程枚举抖动导致的误触发）。
    /// 暂停期间 <see cref="PollOnce"/> 直接返回间隔、不采集快照。
    /// </summary>
    public void PauseFor(TimeSpan duration)
        => Volatile.Write(ref _pausedUntilTicks, DateTimeOffset.UtcNow.Add(duration).UtcTicks);

    /// <summary>
    /// 构建监测循环。
    /// </summary>
    /// <param name="configProvider">配置源（M3c 引导用默认配置；M3d 由 ConfigChanged 广播替换为最新实例）。</param>
    /// <param name="snapshotProvider">快照源（Overlay 侧 Win32 实现；测试侧假实现）。</param>
    /// <param name="selfExecutableDirectory">本程序可执行文件目录（AC-45 同目录判定）。</param>
    /// <param name="onTrigger">命中回调（应触发的大字，架构 §4.4：此处为应入播放队列候选）。</param>
    /// <param name="onSuppression">抑制回调（DedupeWindow 正常语义 / MinInterval 记 MON-W-5005，由日志端区分）。</param>
    /// <param name="onError">快照采集异常回调（对应 MON-E-5001，由日志端登记）。</param>
    /// <param name="onFullscreenChanged">全屏演示/投屏状态变化回调（AC-90：存在真实全屏窗口时 true）。</param>
    public MonitorLoop(
        Func<AppConfig> configProvider,
        IMonitorSnapshotProvider snapshotProvider,
        string selfExecutableDirectory,
        Action<MonitorTrigger> onTrigger,
        Action<MonitorSuppression>? onSuppression = null,
        Action<string>? onError = null,
        Action<bool>? onFullscreenChanged = null)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _selfExecutableDirectory = selfExecutableDirectory
            ?? throw new ArgumentNullException(nameof(selfExecutableDirectory));
        _onTrigger = onTrigger ?? throw new ArgumentNullException(nameof(onTrigger));
        _onSuppression = onSuppression;
        _onError = onError;
        _onFullscreenChanged = onFullscreenChanged;
    }

    /// <summary>
    /// 单轮轮询（可独立测）：配置 → 引擎重建（引用变化时）→ 快照 → 目标构建 → 评估 → 回调分发。
    /// 返回下一轮间隔毫秒（读 <c>global.monitorPollIntervalMs</c>）。
    /// 快照异常不抛出：记 onError 并照常返回间隔（下一轮重试，MON-E-5001 监控检测失败）。
    /// </summary>
    public int PollOnce()
    {
        var config = _configProvider();
        if (DateTimeOffset.UtcNow.UtcTicks < Volatile.Read(ref _pausedUntilTicks))
        {
            return config.Global.MonitorPollIntervalMs; // 暂停窗口内（睡眠唤醒抖动抑制，AC-41）
        }

        if (_engine is null || !ReferenceEquals(config, _appliedConfig))
        {
            _engine = new MonitorEngine(config.Rules, config.Global, _selfPids);
            _appliedConfig = config;
        }

        MonitorSnapshot snapshot;
        try
        {
            snapshot = _snapshotProvider.TakeSnapshot();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex.Message);
            return config.Global.MonitorPollIntervalMs;
        }

        RefreshSelfPids(snapshot);

        // 投屏/演示状态（AC-90）：存在真实全屏窗口即视为演示/投屏中，状态变化时回调
        if (_onFullscreenChanged is not null)
        {
            var anyFullscreen = snapshot.Windows.Any(static w => w.IsFullscreen);
            if (_lastFullscreen != anyFullscreen)
            {
                _lastFullscreen = anyFullscreen;
                _onFullscreenChanged(anyFullscreen);
            }
        }

        var targets = BuildTargets(snapshot);
        var livePids = snapshot.Processes.Select(static p => p.Pid).ToHashSet();
        var evaluation = _engine.Evaluate(targets, livePids, DateTimeOffset.UtcNow);

        foreach (var trigger in evaluation.Triggers)
        {
            _onTrigger(trigger);
        }

        if (_onSuppression is not null)
        {
            foreach (var suppression in evaluation.Suppressions)
            {
                _onSuppression(suppression);
            }
        }

        return config.Global.MonitorPollIntervalMs;
    }

    /// <summary>周期运行直至取消（Cancel 后立即返回）。</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var intervalMs = PollOnce();
            try
            {
                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>AC-45：按「可执行文件目录 == 自程序目录」重建自身 PID 集合（每次快照后刷新）。</summary>
    private void RefreshSelfPids(MonitorSnapshot snapshot)
    {
        _selfPids.Clear();
        var selfDir = Path.TrimEndingDirectorySeparator(_selfExecutableDirectory);
        foreach (var process in snapshot.Processes)
        {
            if (process.ExecutablePath is null)
            {
                continue; // 权限不足拿不到路径 → 不判自身（宁可保留为候选目标，引擎另有 PID 过滤兜底）
            }

            var dir = Path.GetDirectoryName(process.ExecutablePath);
            if (dir is not null &&
                string.Equals(Path.TrimEndingDirectorySeparator(dir), selfDir, StringComparison.OrdinalIgnoreCase))
            {
                _selfPids.Add(process.Pid);
            }
        }
    }

    /// <summary>构建候选目标：进程名目标 + 窗口标题目标；进程目标携带「该进程是否有全屏窗口」标志（AC-05 传播）。</summary>
    private static List<MatchTarget> BuildTargets(MonitorSnapshot snapshot)
    {
        var targets = new List<MatchTarget>(snapshot.Processes.Count + snapshot.Windows.Count);
        var fullscreenPids = snapshot.Windows
            .Where(static w => w.IsFullscreen)
            .Select(static w => w.OwnerPid)
            .ToHashSet();

        foreach (var process in snapshot.Processes)
        {
            if (string.IsNullOrWhiteSpace(process.Name))
            {
                continue;
            }

            targets.Add(MatchTarget.ForProcess(
                process.Name,
                isFullscreen: fullscreenPids.Contains(process.Pid),
                process.Pid));
        }

        foreach (var window in snapshot.Windows)
        {
            if (string.IsNullOrWhiteSpace(window.Title))
            {
                continue;
            }

            targets.Add(MatchTarget.ForWindow(window.Title, window.IsFullscreen, window.OwnerPid));
        }

        return targets;
    }
}