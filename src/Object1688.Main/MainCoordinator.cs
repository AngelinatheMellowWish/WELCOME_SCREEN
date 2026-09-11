using System.IO;
using System.Text.Json;
using System.Windows;
using Object1688.Shared;
using Object1688.Shared.Cli;
using Object1688.Shared.Config;
using Object1688.Shared.Diagnostics;
using Object1688.Shared.Ipc;
using Object1688.Shared.Logging;
using Object1688.Shared.Monitor;
using Object1688.Shared.Perf;
using Object1688.Shared.Text;

namespace Object1688.Main;

/// <summary>
/// Main 进程协调器（架构 §2.4 / M1）。
/// 职责：MainControlPipe 服务端（握手校验 AC-70）、子进程按序拉起、心跳监护与指数退避重启
/// （PRC-I-2003，上限 IpcProtocol.RestartBackoffCap）、优雅退出广播与强制收尾（PRC-E-2002）、
/// 二次实例参数转发受理（AC-78）、SMOKE 冒烟钩子、启动自检降级（AC-82 / GEN-W-9004）。
/// </summary>
internal sealed class MainCoordinator
{
    private readonly CliOptions _cli;
    private readonly string _selfDir;
    private readonly IpcHandshake _selfHandshake;
    private readonly IpcPipeServer _server;
    private readonly IpcPipeClient _logClient;
    private readonly Dictionary<IpcRole, ChildSupervisor> _children = new();
    private readonly HashSet<IpcRole> _heartbeatSeen = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly FrameRateTracker _frameRate = new();
    private int _shutdownStarted;
    private volatile bool _shuttingDown;
    private int _exitCode;

    /// <summary>当前生效配置（启动时加载；Overlay 连接时经 ConfigChanged 广播；ConfigUI 保存后经 ConfigChanged 上报更新）。</summary>
    private volatile AppConfig _config = ConfigLoader.CreateDefault();

    /// <summary>当前配置文件路径（--config 或 %APPDATA%\Object1688\config.json）。</summary>
    private string? _configPath;

    /// <summary>信箱序号（每次下发信封单调递增）。</summary>
    private long _seqCounter;

    /// <summary>欢迎大字是否已触发（Interlocked 保证仅一次，F-11）。</summary>
    private int _welcomeSent;

    /// <summary>Overlay 大字是否正在显示（F-07/AC-30：由 BannerState 上报维护，供快捷键"再次按下提前结束"裁决）。</summary>
    private volatile bool _bannerPlaying;

    /// <summary>是否检测到全屏演示/投屏（AC-90：由 PresentationState 上报维护）。</summary>
    private volatile bool _presentationActive;

    /// <summary>帧率上报链路首次建立是否已记录（一次性诊断日志，AC-68）。</summary>
    private int _frameRateRelayLogged;

    /// <summary>投屏/演示状态链路首次建立是否已记录（一次性诊断日志，AC-90）。</summary>
    private int _presentationStateLogged;

    /// <summary>渲染开销链路首次建立是否已记录（一次性诊断日志，AC-95）。</summary>
    private int _renderCostRelayLogged;

    /// <summary>配置更新事件（App 用于热键重注册等；在 <c>_config</c> 引用替换后触发）。</summary>
    public event Action? ConfigUpdated;

    /// <summary>致命错误回调（AC-35/AC-84：托盘气泡层，由 App 注入并负责去重聚合）。</summary>
    private readonly Action<string, string>? _fatalNotify;

    /// <summary>警告提示回调（F-42/NFR-04 扩展：Overlay 告警如 OVL-W-3008，由 App 去重弹托盘气泡）。</summary>
    private readonly Action<string, string>? _warnNotify;

    /// <summary>外部脚本控制接口服务端（F-76/AC-97）。</summary>
    private readonly ControlServer _controlServer;

    /// <summary>打开配置窗口回调（App 层注入，控制命令 config）。</summary>
    private readonly Action? _openConfigUi;

    /// <summary>打开关于窗口回调（App 层注入，控制命令 about）。</summary>
    private readonly Action? _showAbout;

    /// <summary>初始化并装配协调器（不含启动）。</summary>
    /// <param name="cli">已解析的命令行选项。</param>
    /// <param name="fatalNotify">致命（ERROR/FATAL）错误通知回调（errorCode, message）；由 UI 层注入后经去重弹托盘气泡。</param>
    /// <param name="openConfigUi">打开配置窗口回调（控制命令 config）。</param>
    /// <param name="showAbout">打开关于窗口回调（控制命令 about）。</param>
    /// <param name="warnNotify">子进程警告提示回调（errorCode, message），由 App 去重弹托盘气泡。</param>
    public MainCoordinator(
        CliOptions cli,
        Action<string, string>? fatalNotify = null,
        Action? openConfigUi = null,
        Action? showAbout = null,
        Action<string, string>? warnNotify = null)
    {
        _cli = cli;
        _fatalNotify = fatalNotify;
        _openConfigUi = openConfigUi;
        _showAbout = showAbout;
        _warnNotify = warnNotify;
        _selfDir = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? string.Empty;
        _selfHandshake = new IpcHandshake
        {
            Role = IpcRole.Main,
            Pid = Environment.ProcessId,
            ExecutablePath = Environment.ProcessPath ?? string.Empty,
        };

        _server = new IpcPipeServer(IpcProtocol.MainControlPipe, ValidatePeer);
        _server.MessageReceived += OnServerMessage;
        _server.ClientConnected += (_, e) =>
        {
            // Overlay 就绪：广播当前配置 + F-11 欢迎大字（架构 §5.7/欢迎时序）
            if (e.Handshake?.Role == IpcRole.Overlay)
            {
                _ = SendStartupStateToOverlayAsync(_lifetimeCts.Token);
            }
        };
        _server.AcceptLoopFaulted += (_, ex) =>
            _ = LogAsync(LogLevel.Error, $"MainControlPipe 接受循环异常（已自动续跑）：{ex.Message}", ErrorCodes.IpcCommunicationFailed);

        // 外部脚本控制接口（F-76/AC-97）：独立控制管道，仅当前用户可访问
        _controlServer = new ControlServer(HandleControlAsync);
        _controlServer.AcceptLoopFaulted += (_, ex) =>
            _ = LogAsync(LogLevel.Error, $"控制接口接受循环异常（已自动续跑）：{ex.Message}", ErrorCodes.IpcCommunicationFailed);

        _logClient = new IpcPipeClient(IpcProtocol.LoggingPipe, _selfHandshake);

        _children.Add(IpcRole.Logging, new ChildSupervisor(IpcRole.Logging, "Object1688.Logging.exe"));
        _children.Add(IpcRole.Overlay, new ChildSupervisor(IpcRole.Overlay, "Object1688.Overlay.exe"));
        _children.Add(IpcRole.Monitor, new ChildSupervisor(IpcRole.Monitor, "Object1688.Monitor.exe"));
    }

    /// <summary>是否处于冒烟测试模式（环境变量 OBJECT1688_SMOKE=1）。</summary>
    public bool SmokeRequested => Environment.GetEnvironmentVariable("OBJECT1688_SMOKE") == "1";

    /// <summary>获取进程最终退出码（正常 0；SMOKE 心跳未就绪 1）。</summary>
    public int ExitCode => _exitCode;

    /// <summary>
    /// 启动全部后台任务：日志通道、MainControlPipe 服务端、子进程按序拉起、监督循环、SMOKE 钩子。
    /// </summary>
    public Task StartAsync()
    {
        _ = _logClient.RunAsync(_lifetimeCts.Token);
        LoadConfig();
        _server.Start();
        _controlServer.Start();
        _ = Task.Run(() => StartChildrenSequentiallyAsync(_lifetimeCts.Token));
        _ = Task.Run(() => SuperviseLoopAsync(_lifetimeCts.Token));
        if (SmokeRequested)
        {
            _ = Task.Run(() => SmokeAsync(_lifetimeCts.Token));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 请求 Monitor 打开性能窗口（架构 §6.3 / 托盘"性能窗口…"入口，AC-10）。
    /// 通过 MainControlPipe 下发 ShowPerfWindow（无载荷）；Monitor 若窗口已隐藏则 Show/激活。
    /// </summary>
    public async Task RequestPerfWindowAsync()
    {
        try
        {
            var envelope = IpcEnvelope.Create(IpcMessageType.ShowPerfWindow, NextSeq(), null);
            await _server.SendToRoleAsync(IpcRole.Monitor, envelope, _lifetimeCts.Token);
            await LogAsync(LogLevel.Info, "已请求 Monitor 打开性能窗口");
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"请求性能窗口失败：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }
    }

    /// <summary>
    /// 触发手动大字（托盘"手动大字"入口 / 快捷键，F-12/AC-29）。
    /// 强制路径不受勿扰/暂停静默（F-25 扩展）；手动大字未启用或文字为空时忽略并提示。
    /// </summary>
    public async Task TriggerManualBigTextAsync()
    {
        try
        {
            if (!_config.Manual.Enabled)
            {
                await LogAsync(LogLevel.Warn, "手动大字未启用，忽略触发");
                return;
            }

            if (_config.Manual.DisplayLines.Count == 0)
            {
                await LogAsync(LogLevel.Warn, "手动大字文字为空，忽略触发");
                return;
            }

            var request = BannerAssembler.ComposeManual(_config.Manual, _config.Global, _config.Sound);
            await SendBannerToOverlayAsync(request, null, _lifetimeCts.Token);
            await LogAsync(LogLevel.Info, "手动大字指令已下发 Overlay");
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"手动大字触发失败：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }
    }

    /// <summary>当前是否为首次运行（F-13/AC-20 引导依据）。</summary>
    public bool IsFirstRun => _config.FirstRun;

    /// <summary>手动大字全局快捷键（F-12；null/空 = 未启用）。</summary>
    public string? ManualShortcut => _config.Manual.Shortcut;

    /// <summary>当前是否处于全局暂停（托盘"暂停/恢复"与状态提示用，F-25）。</summary>
    public bool IsPaused => _config.Dnd.Paused;

    /// <summary>当前界面语言（配置 language；null = 跟随系统，F-60）。</summary>
    public string? Language => _config.Language;

    /// <summary>切换全局暂停（托盘"暂停/恢复"，F-25）：持久化 + 广播热生效。</summary>
    public async Task TogglePauseAsync() => await SetPausedAsync(!_config.Dnd.Paused);

    /// <summary>当前自动触发是否处于静默（暂停/定时勿扰/投屏演示），供托盘状态展示（F-50/AC-62）。</summary>
    public bool IsAutoSilenced()
        => DndGate.IsSuppressed(_config.Dnd, DateTimeOffset.Now, isForced: false, presentationActive: _presentationActive);

    /// <summary>
    /// 自启路径健康检查（F-24/AC-53）：配置开启自启且注册项指向的 exe 与当前不一致 → 记 PRC-W-2006 并返回失效路径；否则 null。
    /// 仅检测提示，**不自动改写注册项**（引导用户在配置界面重新确认）。
    /// </summary>
    public string? CheckAutostartInvalidPath()
    {
        if (!_config.Autostart)
        {
            return null;
        }

        try
        {
            var registered = AutostartRegistry.GetRegisteredPath();
            var current = Environment.ProcessPath;
            if (registered is null || string.IsNullOrEmpty(current))
            {
                return null; // 未注册 / 无法取当前路径：非"路径失效"场景
            }

            if (AutostartRegistry.IsRegisteredPathValid(current))
            {
                return null;
            }

            _ = LogAsync(
                LogLevel.Warn,
                $"开机自启路径失效：注册项指向 {registered}，当前 exe 为 {current}（请在配置界面重新确认）",
                ErrorCodes.AutostartPathInvalid);
            return registered;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>大字是否正在显示（F-07/AC-30）。</summary>
    public bool IsBannerPlaying => _bannerPlaying;

    /// <summary>
    /// 手动触发快捷键按下（F-07/AC-30）：正在显示大字 → 提前结束当前大字；否则 → 触发手动大字。
    /// </summary>
    public async Task OnManualHotkeyAsync()
    {
        if (_bannerPlaying)
        {
            await EndBannerAsync(_lifetimeCts.Token);
            await LogAsync(LogLevel.Info, "手动快捷键：提前结束当前大字（F-07）");
        }
        else
        {
            await TriggerManualBigTextAsync();
            await LogAsync(LogLevel.Info, "手动快捷键：触发手动大字（F-12）");
        }
    }

    /// <summary>
    /// 确认首次运行引导已完成：清除 firstRun 标志并原子写盘（失败仅记日志不阻断）。
    /// </summary>
    public async Task AcknowledgeFirstRunAsync()
    {
        if (!_config.FirstRun || string.IsNullOrEmpty(_configPath))
        {
            return;
        }

        var updated = RebaseConfig(firstRun: false, language: null);
        try
        {
            ConfigSaver.Save(_configPath, updated);
            SetConfig(updated);
            await BroadcastConfigChangedAsync();
        }
        catch (IOException ex)
        {
            await LogAsync(LogLevel.Warn, $"清除首次运行标志失败：{ex.Message}", ErrorCodes.ConfigWriteFailed);
        }
    }

    /// <summary>
    /// 切换界面语言（托盘"切换语言"，AC-09/F-60）：zh-CN ↔ en-US 轮换，写回配置并广播热生效。
    /// </summary>
    public async Task ToggleLanguageAsync()
    {
        var next = string.Equals(_config.Language, "en-US", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
        var updated = RebaseConfig(firstRun: null, language: next);
        try
        {
            if (!string.IsNullOrEmpty(_configPath))
            {
                ConfigSaver.Save(_configPath, updated);
            }

            SetConfig(updated);
            await BroadcastConfigChangedAsync();
            await LogAsync(LogLevel.Info, $"界面语言已切换为 {next}（ConfigChanged 已广播）");
        }
        catch (IOException ex)
        {
            await LogAsync(LogLevel.Warn, $"切换语言写盘失败：{ex.Message}", ErrorCodes.ConfigWriteFailed);
        }
    }

    /// <summary>更新内存配置快照并通知订阅者（App 用于全局热键重注册等，F-12/AC-71）。</summary>
    private void SetConfig(AppConfig config)
    {
        _config = config;
        ConfigUpdated?.Invoke();
    }

    /// <summary>基于当前配置重建 AppConfig（仅覆盖指定字段，其余保持原引用）。</summary>
    private AppConfig RebaseConfig(bool? firstRun, string? language)
    {
        var c = _config;
        return new AppConfig
        {
            SchemaVersion = c.SchemaVersion,
            Language = language ?? c.Language,
            Global = c.Global,
            Welcome = c.Welcome,
            Manual = c.Manual,
            Rules = c.Rules,
            Dnd = c.Dnd,
            Sound = c.Sound,
            Autostart = c.Autostart,
            FirstRun = firstRun ?? c.FirstRun,
            UiState = c.UiState,
        };
    }

    /// <summary>向 Overlay 广播当前配置（ConfigChanged 热生效）。</summary>
    private async Task BroadcastConfigChangedAsync()
    {
        var broadcast = IpcEnvelope.Create(
            IpcMessageType.ConfigChanged,
            NextSeq(),
            JsonSerializer.SerializeToElement(_config, IpcJson.Options));
        await _server.SendToRoleAsync(IpcRole.Overlay, broadcast, _lifetimeCts.Token);
    }

    /// <summary>
    /// 系统电源/会话事件下发（架构 §2.4）：Main 监听后转发 Overlay（睡眠/唤醒/锁屏/解锁），
    /// 睡眠/唤醒同时转发 Monitor（AC-41/AC-50/AC-76）。
    /// </summary>
    public async Task NotifySystemEventAsync(SystemEventKind kind)
    {
        try
        {
            var envelope = IpcEnvelope.Create(
                IpcMessageType.SystemEvent,
                NextSeq(),
                JsonSerializer.SerializeToElement(new SystemEventReport { Kind = kind }, IpcJson.Options));
            await _server.SendToRoleAsync(IpcRole.Overlay, envelope, _lifetimeCts.Token);
            if (kind is SystemEventKind.Suspend or SystemEventKind.Resume)
            {
                await _server.SendToRoleAsync(IpcRole.Monitor, envelope, _lifetimeCts.Token);
            }

            await LogAsync(LogLevel.Info, $"系统事件：{kind}（已下发子进程）");
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"系统事件下发失败：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }
    }

    /// <summary>
    /// 优雅退出（幂等）：广播 Shutdown → 等待子进程回执退出（SessionEnding 2s，其余 3s）
    /// → 超时强制 Kill（PRC-E-2002）→ 关闭管道 → 结束 WPF 消息循环。
    /// </summary>
    public async Task RequestShutdownAsync(ShutdownReason reason)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
        {
            return;
        }

        _shuttingDown = true;
        await LogAsync(LogLevel.Info, $"主进程开始优雅退出（原因：{reason}）");

        var envelope = IpcEnvelope.Create(
            IpcMessageType.Shutdown,
            0,
            JsonSerializer.SerializeToElement(reason, IpcJson.Options));
        try
        {
            await _server.BroadcastAsync(envelope);
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"广播退出指令失败：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }

        var grace = reason == ShutdownReason.SessionEnding
            ? IpcProtocol.SessionEndingFlushTimeout
            : IpcProtocol.ShutdownGraceTimeout;
        var deadline = DateTimeOffset.UtcNow + grace;
        while (DateTimeOffset.UtcNow < deadline && _children.Values.Any(c => c.IsRunning))
        {
            await Task.Delay(100);
        }

        foreach (var (role, child) in _children)
        {
            if (child.IsRunning)
            {
                await LogAsync(LogLevel.Warn, $"子进程 {role} 未在时限内退出，强制终止", ErrorCodes.ChildProcessAbnormalExit);
                child.Kill();
            }
            else
            {
                child.MarkGracefulExit();
            }
        }

        await LogAsync(LogLevel.Info, "主进程退出完成");

        // 资源清理各自独立 try/catch：任一失败都不阻断后续清理与最终退出（PRC-E-2002 语义下沉到 OS）
        try
        {
            await _server.DisposeAsync();
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"主控制管道关闭异常（不影响退出）：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }

        try
        {
            await _controlServer.DisposeAsync();
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"控制管道关闭异常（不影响退出）：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }

        try
        {
            await _logClient.DisposeAsync();
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"日志通道关闭异常（不影响退出）：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }

        _lifetimeCts.Cancel();

        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(
            () => System.Windows.Application.Current.Shutdown(_exitCode));
    }

    /// <summary>按架构 §2.4 顺序拉起子进程（Logging → Overlay → Monitor，不等待就绪）。</summary>
    private async Task StartChildrenSequentiallyAsync(CancellationToken ct)
    {
        foreach (var role in new[] { IpcRole.Logging, IpcRole.Overlay, IpcRole.Monitor })
        {
            if (_shuttingDown || ct.IsCancellationRequested)
            {
                return;
            }

            var child = _children[role];
            if (child.TryStart(out var reason))
            {
                await LogAsync(LogLevel.Info, $"子进程 {role} 已启动");
            }
            else
            {
                await LogAsync(LogLevel.Error, $"子进程 {role} 启动失败：{reason}", ErrorCodes.ChildProcessStartFailed);
                child.ScheduleNextTry(); // 交由监督循环重试
            }
        }
    }

    /// <summary>
    /// 监督循环（1s 周期）：心跳失联（IPC-W-7002）判定与重启、异常退出（PRC-E-2002）侦测、
    /// 退避窗口到期后拉起（PRC-I-2003）。
    /// </summary>
    private async Task SuperviseLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            foreach (var (role, child) in _children)
            {
                if (_shuttingDown)
                {
                    return;
                }

                if (child.IsRunning && DateTimeOffset.UtcNow - child.LastHeartbeat > IpcProtocol.HeartbeatTimeout)
                {
                    await LogAsync(LogLevel.Warn, $"子进程 {role} 心跳超时，强制重启", ErrorCodes.IpcHeartbeatTimeout);
                    child.Kill();
                    child.ScheduleNextTry();
                }

                if (!child.IsRunning && child.Process is { HasExited: true })
                {
                    await LogAsync(LogLevel.Warn, $"子进程 {role} 异常退出（代码 {child.Process.ExitCode}）", ErrorCodes.ChildProcessAbnormalExit);
                    child.ScheduleNextTry();
                }

                if (!child.IsRunning && child.RestartDue)
                {
                    if (child.TryStart(out var reason))
                    {
                        await LogAsync(LogLevel.Info, $"子进程 {role} 已重启（第 {child.RestartAttempt} 次）", ErrorCodes.ChildProcessRestarted);
                    }
                    else
                    {
                        await LogAsync(LogLevel.Error, $"子进程 {role} 启动失败：{reason}", ErrorCodes.ChildProcessStartFailed);
                    }
                }
            }
        }
    }

    /// <summary>冒烟钩子：等待全部子进程真实心跳（最多 10s），就绪记成功、否则 FAIL（GEN-W-9004），随后优雅退出。</summary>
    private async Task SmokeAsync(CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            lock (_heartbeatSeen)
            {
                if (SeenAllHeartbeats())
                {
                    break;
                }
            }

            await Task.Delay(200, ct);
        }

        var allReady = SeenAllHeartbeats();
        if (allReady)
        {
            await LogAsync(LogLevel.Info, "SMOKE：全部子进程心跳就绪，触发优雅退出");
            _exitCode = 0;
        }
        else
        {
            await LogAsync(LogLevel.Warn, "SMOKE：部分子进程心跳未就绪（10s 超时），按失败退出", ErrorCodes.GenericDegradedMode);
            _exitCode = 1;
        }

        await RequestShutdownAsync(ShutdownReason.UserExit);
    }

    private bool SeenAllHeartbeats()
    {
        lock (_heartbeatSeen)
        {
            return _heartbeatSeen.SetEquals(_children.Keys);
        }
    }

    /// <summary>启动自检 + 配置加载（AC-82）：--config 优先，否则 %APPDATA%\Object1688\config.json；
    /// 缺失回退模板/内置默认（CFG-W-1002），模板也缺失 → GEN-W-9004 降级运行，不阻断启动。</summary>
    private void LoadConfig()
    {
        _configPath = _cli.ConfigPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Object1688", "config.json");
        var result = ConfigLoader.Load(_configPath);
        SetConfig(result.Config);
        foreach (var issue in result.Issues)
        {
            _ = LogAsync(LogLevel.Warn, $"配置加载：{issue.Message}", issue.ErrorCode);
        }

        if (result.Issues.Any(i => i.ErrorCode == ErrorCodes.ConfigDefaulted))
        {
            _ = LogAsync(LogLevel.Warn, "启动自检失败：配置文件缺失，进入降级模式", ErrorCodes.GenericDegradedMode);
        }
    }

    /// <summary>
    /// Overlay 就绪推送（架构 §5.7 欢迎时序 + 配置链路）：先广播 ConfigChanged（当前配置快照），
    /// 再按 F-11 触发欢迎大字（仅一次，Interlocked 防并发重入）。
    /// </summary>
    private async Task SendStartupStateToOverlayAsync(CancellationToken ct)
    {
        try
        {
            var configEnvelope = IpcEnvelope.Create(
                IpcMessageType.ConfigChanged,
                NextSeq(),
                JsonSerializer.SerializeToElement(_config, IpcJson.Options));
            await _server.SendToRoleAsync(IpcRole.Overlay, configEnvelope, ct);
            await LogAsync(LogLevel.Info, "Overlay 已就绪，ConfigChanged 配置已广播");

            if (_config.Welcome.Enabled && Interlocked.CompareExchange(ref _welcomeSent, 1, 0) == 0)
            {
                var welcome = BannerAssembler.ComposeWelcome(_config.Welcome, _config.Global, _config.Sound);
                await SendBannerToOverlayAsync(welcome, null, ct);
                await LogAsync(LogLevel.Info, "欢迎大字已触发（F-11）");
            }
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"Overlay 就绪推送失败：{ex.Message}", ErrorCodes.IpcCommunicationFailed);
        }
    }

    /// <summary>
    /// 触发分发（架构 §4.4 M3d）：Monitor 命中上报 TriggerEvent → 按 RuleId 查规则（配置可能已变更，
    /// 查无 → MON-W-5006 丢弃）→ BannerAssembler 合并组装 BannerRequest → TriggerCommand 定向回推 Overlay。
    /// </summary>
    private async Task HandleTriggerEventAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        var report = envelope.GetPayload<TriggerEventReport>();
        if (report is null)
        {
            await LogAsync(LogLevel.Warn, "收到 TriggerEvent 但载荷解析失败（TriggerEventReport 为 null），已忽略");
            return;
        }

        var rule = _config.Rules.FirstOrDefault(r => r.RuleId == report.RuleId);
        if (rule is null)
        {
            await LogAsync(LogLevel.Warn, $"命中上报的规则不存在（配置可能已变更）：{report.RuleId}", ErrorCodes.MonitorRuleNotFound);
            return;
        }

        // 勿扰/暂停常驻裁决（F-25/AC-31/AC-90）：自动触发处于全局暂停、定时勿扰或投屏演示静默 → 抑制不弹大字（事件已记录，仍上报过 TriggerEvent）
        if (DndGate.IsSuppressed(_config.Dnd, DateTimeOffset.Now, isForced: false, presentationActive: _presentationActive))
        {
            await LogAsync(LogLevel.Warn, $"规则 [{rule.RuleId}] 命中但处于勿扰/暂停时段，大字已抑制（事件照常记录）：{report.MatchedText}");
            return;
        }

        var request = BannerAssembler.ComposeRule(
            rule, _config.Global, _config.Sound, report.MatchedText, report.TriggeredAtUtc);
        await SendBannerToOverlayAsync(request, rule.RuleId, ct);
        await LogAsync(LogLevel.Info, $"{ErrorCodes.MonitorRuleQueued} 规则 [{rule.RuleId}] 命中：{report.MatchedText}，大字指令已下发 Overlay");
    }

    /// <summary>
    /// ConfigUI 保存后配置上报（架构 §5.5 F-21/AC-46）：ConfigUI 已完成磁盘原子写（ConfigSaver），
    /// Main 更新内存快照并把 ConfigChanged 广播 Overlay（热生效，Overlay 侧监测引擎按引用重建）。
    /// 载荷为 AppConfig 快照；解析失败忽略并告警。
    /// </summary>
    private async Task HandleConfigChangedFromUiAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        var config = envelope.GetPayload<AppConfig>();
        if (config is null)
        {
            await LogAsync(LogLevel.Warn, "收到 ConfigUI 的 ConfigChanged 但载荷解析失败（AppConfig 为 null），已忽略");
            return;
        }

        SetConfig(config); // 引用替换：内存配置生效（磁盘已由 ConfigUI 原子写完成）
        var broadcast = IpcEnvelope.Create(
            IpcMessageType.ConfigChanged,
            NextSeq(),
            JsonSerializer.SerializeToElement(_config, IpcJson.Options));
        await _server.SendToRoleAsync(IpcRole.Overlay, broadcast, ct);
        await LogAsync(LogLevel.Info, "ConfigUI 配置已保存并生效，ConfigChanged 已广播 Overlay");
    }

    /// <summary>
    /// ConfigUI"测试显示"（架构 §5.6 F-23）：载荷为 BannerRequest（displayLines/targetScreen 全字段），
    /// Main 校验后转 TriggerCommand 下发 Overlay 做一次真实播放（不走规则匹配，仅手动预览）。
    /// </summary>
    private async Task HandleTestPlayAsync(IpcEnvelope envelope, CancellationToken ct)
    {
        var request = envelope.GetPayload<BannerRequest>();
        if (request is null)
        {
            await LogAsync(LogLevel.Warn, "收到 TestPlay 但载荷解析失败（BannerRequest 为 null），已忽略");
            return;
        }

        await SendBannerToOverlayAsync(request, null, ct);
        await LogAsync(LogLevel.Info, "ConfigUI 测试显示已下发 Overlay（TestPlay → TriggerCommand）");
    }

    /// <summary>信封序号（单调递增）。</summary>
    private long NextSeq() => Interlocked.Increment(ref _seqCounter);

    // ===== 外部脚本控制接口（F-76/F-77，AC-97/98/99）=====

    /// <summary>控制命令分发：按命令名执行并返回 JSON 响应；未知命令/参数错误返回 IPC-E-7005，不抛异常。</summary>
    private async Task<ControlResponse> HandleControlAsync(ControlRequest request, CancellationToken ct)
    {
        var command = request.Command.Trim().ToLowerInvariant();
        switch (command)
        {
            case ControlProtocol.Commands.Ping:
                return ControlResponse.Success(JsonSerializer.SerializeToElement(new { pong = true }, IpcJson.Options));

            case ControlProtocol.Commands.Status:
                return ControlResponse.Success(BuildStatusData());

            case ControlProtocol.Commands.Banner:
                return await ShowControlBannerAsync(request, ct);

            case ControlProtocol.Commands.Manual:
                if (!_config.Manual.Enabled)
                {
                    return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "手动大字未启用（manual.enabled=false）");
                }

                await TriggerManualBigTextAsync();
                return ControlResponse.Success();

            case ControlProtocol.Commands.Pause:
                await SetPausedAsync(true);
                return ControlResponse.Success();

            case ControlProtocol.Commands.Resume:
                await SetPausedAsync(false);
                return ControlResponse.Success();

            case ControlProtocol.Commands.TogglePause:
                await SetPausedAsync(!_config.Dnd.Paused);
                return ControlResponse.Success(JsonSerializer.SerializeToElement(new { paused = _config.Dnd.Paused }, IpcJson.Options));

            case ControlProtocol.Commands.End:
                await EndBannerAsync(ct);
                return ControlResponse.Success();

            case ControlProtocol.Commands.Reload:
                LoadConfig();
                await BroadcastConfigChangedAsync();
                await LogAsync(LogLevel.Info, "控制接口：配置已从磁盘重载并广播");
                return ControlResponse.Success();

            case ControlProtocol.Commands.Config:
                if (_openConfigUi is null)
                {
                    return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "配置窗口入口不可用");
                }

                _openConfigUi();
                return ControlResponse.Success();

            case ControlProtocol.Commands.Perf:
                await RequestPerfWindowAsync();
                return ControlResponse.Success();

            case ControlProtocol.Commands.About:
                if (_showAbout is null)
                {
                    return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "关于窗口入口不可用");
                }

                _showAbout();
                return ControlResponse.Success();

            case ControlProtocol.Commands.Diagnostics:
                return await ExportDiagnosticsAsync();

            case ControlProtocol.Commands.Quit:
                _ = RequestShutdownAsync(ShutdownReason.UserExit);
                return ControlResponse.Success();

            default:
                return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, $"未知命令：{request.Command}");
        }
    }

    /// <summary>控制命令 banner：以请求参数（缺省回退全局）组装 BannerRequest 并下发 Overlay 显示。</summary>
    private async Task<ControlResponse> ShowControlBannerAsync(ControlRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "banner 命令需要 text 参数");
        }

        var lines = request.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(t => new DisplayLine { Text = t })
            .ToList();
        if (lines.All(l => string.IsNullOrWhiteSpace(l.Text)))
        {
            return ControlResponse.Failure(ErrorCodes.IpcControlCommandInvalid, "banner 命令 text 为空");
        }

        var global = _config.Global;
        var banner = new BannerRequest
        {
            DisplayLines = lines,
            FontSize = request.FontSize is > 0 ? request.FontSize.Value : global.DefaultFontSize,
            Color = "#FFFFFF",
            OutlineColor = string.IsNullOrWhiteSpace(request.OutlineColor) ? global.DefaultOutlineColor : request.OutlineColor,
            OutlineWidth = request.OutlineWidth ?? global.DefaultOutlineWidth,
            OutlineMode = ResolveOutlineMode(request.OutlineMode, global.OutlineMode),
            Position = global.DefaultPosition,
            TargetScreen = string.IsNullOrWhiteSpace(request.TargetScreen) ? global.TargetScreen : request.TargetScreen,
            DelaySeconds = 0,
            HoldSeconds = request.HoldSeconds ?? 4,
            WrapStrategy = WrapStrategy.Wrap,
        };

        await SendBannerToOverlayAsync(banner, null, ct);
        await LogAsync(LogLevel.Info, "控制接口：banner 大字指令已下发 Overlay");
        return ControlResponse.Success();
    }

    /// <summary>导出一键诊断包（F-30 扩展/AC-83）：日志 + 配置 + 统计 + 环境信息 → log/exports/diagnostics-*.zip。</summary>
    public async Task<ControlResponse> ExportDiagnosticsAsync()
    {
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "log");
            var statsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Object1688", "stats.json");
            var version = typeof(MainCoordinator).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            var result = DiagnosticPackager.Create(
                DiagnosticPackager.DefaultOutputDirectory, logDir, _configPath ?? string.Empty, statsPath, version);

            await LogAsync(LogLevel.Info, $"诊断包已导出：{result.Path}{(result.Partial ? "（部分文件缺失）" : string.Empty)}");
            return ControlResponse.Success(JsonSerializer.SerializeToElement(new
            {
                path = result.Path,
                partial = result.Partial,
                warnings = result.Warnings,
            }, IpcJson.Options));
        }
        catch (Exception ex)
        {
            await LogAsync(LogLevel.Warn, $"诊断包导出失败：{ex.Message}", ErrorCodes.IoExportWriteFailed);
            return ControlResponse.Failure(ErrorCodes.IoExportWriteFailed, ex.Message);
        }
    }

    /// <summary>设置全局暂停态（F-25）：持久化配置 + 广播 ConfigChanged 热生效。</summary>
    private async Task SetPausedAsync(bool paused)
    {
        var c = _config;
        var updated = new AppConfig
        {
            SchemaVersion = c.SchemaVersion,
            Language = c.Language,
            Global = c.Global,
            Welcome = c.Welcome,
            Manual = c.Manual,
            Rules = c.Rules,
            Dnd = new DndConfig { Paused = paused, ScheduleEnabled = c.Dnd.ScheduleEnabled, Schedule = c.Dnd.Schedule },
            Sound = c.Sound,
            Autostart = c.Autostart,
            FirstRun = c.FirstRun,
            UiState = c.UiState,
        };

        try
        {
            if (!string.IsNullOrEmpty(_configPath))
            {
                ConfigSaver.Save(_configPath, updated);
            }
        }
        catch (IOException ex)
        {
            await LogAsync(LogLevel.Warn, $"控制接口设置暂停态写盘失败：{ex.Message}", ErrorCodes.ConfigWriteFailed);
        }

        SetConfig(updated);
        await BroadcastConfigChangedAsync();
        await LogAsync(LogLevel.Info, paused ? "控制接口：全局暂停已开启" : "控制接口：全局暂停已关闭");
    }

    /// <summary>提前结束当前大字（F-07）：下发 EndBanner 至 Overlay。</summary>
    private async Task EndBannerAsync(CancellationToken ct)
    {
        var envelope = IpcEnvelope.Create(IpcMessageType.EndBanner, NextSeq(), null);
        await _server.SendToRoleAsync(IpcRole.Overlay, envelope, ct);
        await LogAsync(LogLevel.Info, "控制接口：已下发提前结束大字");
    }

    /// <summary>
    /// 下发大字显示：向 Overlay 发 TriggerCommand，并同步 LastBanner 给 Monitor（F-30 扩展/AC-92 回看）。
    /// 所有大字路径（规则/欢迎/手动/测试/控制接口）统一经此发送，保证回看一致。
    /// </summary>
    private async Task SendBannerToOverlayAsync(BannerRequest request, string? ruleId, CancellationToken ct)
    {
        var command = IpcEnvelope.Create(
            IpcMessageType.TriggerCommand,
            NextSeq(),
            JsonSerializer.SerializeToElement(request, IpcJson.Options));
        await _server.SendToRoleAsync(IpcRole.Overlay, command, ct);

        var last = new LastBannerReport
        {
            Lines = request.DisplayLines.Select(l => l.Text).ToList(),
            RuleId = ruleId,
            Timestamp = DateTimeOffset.UtcNow,
        };
        var lastEnvelope = IpcEnvelope.Create(
            IpcMessageType.LastBanner,
            NextSeq(),
            JsonSerializer.SerializeToElement(last, IpcJson.Options));
        await _server.SendToRoleAsync(IpcRole.Monitor, lastEnvelope, ct);
    }

    /// <summary>构建 status 命令响应数据（版本/暂停态/语言/配置路径/子进程状态）。</summary>
    private JsonElement BuildStatusData()
    {
        var children = _children.ToDictionary(
            kv => kv.Key.ToString(),
            kv => kv.Value.IsRunning ? "running" : "stopped");

        return JsonSerializer.SerializeToElement(new
        {
            running = true,
            version = typeof(MainCoordinator).Assembly.GetName().Version?.ToString(3) ?? "0.1.0",
            paused = _config.Dnd.Paused,
            language = _config.Language,
            configPath = _configPath,
            children,
            fps = _frameRate.HasValue ? _frameRate.Fps : -1.0,
            fpsDegraded = _frameRate.Degraded,
        }, IpcJson.Options);
    }

    /// <summary>解析描边方式（控制命令参数 → 全局默认 → Shadow 兜底）。</summary>
    private static OutlineMode ResolveOutlineMode(string? specific, string? global)
    {
        if (!string.IsNullOrWhiteSpace(specific) && Enum.TryParse<OutlineMode>(specific.Trim(), ignoreCase: true, out var explicitMode))
        {
            return explicitMode;
        }

        return !string.IsNullOrWhiteSpace(global) && Enum.TryParse<OutlineMode>(global.Trim(), ignoreCase: true, out var globalMode)
            ? globalMode
            : OutlineMode.Shadow;
    }

    /// <summary>握手校验（AC-70）：仅放行可执行文件位于本程序输出目录的进程。</summary>
    private bool ValidatePeer(IpcHandshake handshake)
    {
        if (handshake is null)
        {
            return false;
        }

        var peerDir = Path.GetDirectoryName(handshake.ExecutablePath);
        if (string.IsNullOrWhiteSpace(peerDir) || string.IsNullOrWhiteSpace(_selfDir))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(peerDir).TrimEnd('\\'),
            Path.GetFullPath(_selfDir).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>服务端消息分发：心跳更新监护状态、二次实例转发受理、子进程退出回执记录。</summary>
    private void OnServerMessage(object? sender, IpcMessageEventArgs e)
    {
        var role = e.Handshake?.Role;
        switch (e.Envelope.Type)
        {
            case IpcMessageType.Heartbeat when role is not null && _children.TryGetValue(role.Value, out var child):
                child.MarkHeartbeat();
                lock (_heartbeatSeen)
                {
                    _heartbeatSeen.Add(role.Value);
                }

                break;

            case IpcMessageType.RedirectArgs:
                var args = e.Envelope.GetPayload<string[]>() ?? [];
                _ = LogAsync(LogLevel.Info, $"二次实例参数转发（{string.Join(' ', args)}）");
                if (args.Contains("--quit", StringComparer.Ordinal))
                {
                    _ = RequestShutdownAsync(ShutdownReason.UserExit);
                }

                break;

            case IpcMessageType.ShutdownAck when role is not null:
                _ = LogAsync(LogLevel.Info, $"子进程 {role.Value} 回执退出确认");
                break;

            case IpcMessageType.TriggerEvent:
                _ = HandleTriggerEventAsync(e.Envelope, _lifetimeCts.Token);
                break;

            case IpcMessageType.ConfigChanged when role == IpcRole.ConfigUI:
                // ConfigUI 保存后上报（架构 §5.5）：磁盘原子写已由 ConfigUI 侧完成，Main 更新内存快照并广播 Overlay 热生效
                _ = HandleConfigChangedFromUiAsync(e.Envelope, _lifetimeCts.Token);
                break;

            case IpcMessageType.TestPlay when role == IpcRole.ConfigUI:
                // ConfigUI"测试显示"（架构 §5.6 F-23）：载荷为 BannerRequest，转 TriggerCommand 下发 Overlay
                _ = HandleTestPlayAsync(e.Envelope, _lifetimeCts.Token);
                break;

            case IpcMessageType.BannerState when role == IpcRole.Overlay:
                // 大字播放状态（F-07）：供手动快捷键"再次按下提前结束"裁决
                var bannerState = e.Envelope.GetPayload<BannerStateReport>();
                if (bannerState is not null)
                {
                    _bannerPlaying = bannerState.Playing;
                }

                break;

            case IpcMessageType.FrameRate when role == IpcRole.Overlay:
                // 帧率上报（AC-68）：记录最近值（供控制接口 status，AC-67 可机测）+ 转发 Monitor 绘制帧率曲线
                var frameRate = e.Envelope.GetPayload<FrameRateReport>();
                if (frameRate is not null)
                {
                    _frameRate.Update(frameRate.Fps, frameRate.Degraded);
                }

                _ = _server.SendToRoleAsync(IpcRole.Monitor, e.Envelope, _lifetimeCts.Token);
                if (Interlocked.Exchange(ref _frameRateRelayLogged, 1) == 0)
                {
                    _ = LogAsync(LogLevel.Info, "帧率上报链路已建立（Overlay→Main→Monitor，AC-68）");
                }

                break;

            case IpcMessageType.PresentationState when role == IpcRole.Overlay:
                // 投屏/演示状态（AC-90）：供勿扰裁决静默自动触发
                var presentation = e.Envelope.GetPayload<PresentationStateReport>();
                if (presentation is not null)
                {
                    if (Interlocked.Exchange(ref _presentationStateLogged, 1) == 0)
                    {
                        _ = LogAsync(LogLevel.Info, "投屏/演示状态链路已建立（Overlay→Main，AC-90）");
                    }

                    if (presentation.Active != _presentationActive)
                    {
                        _presentationActive = presentation.Active;
                        _ = LogAsync(LogLevel.Info, presentation.Active ? "检测到全屏演示/投屏，进入静默裁决（AC-90）" : "演示/投屏结束，恢复（AC-90）");
                    }
                }

                break;

            case IpcMessageType.RenderCost when role == IpcRole.Overlay:
                // 渲染瞬时开销（AC-95）：转发 Monitor 展示
                _ = _server.SendToRoleAsync(IpcRole.Monitor, e.Envelope, _lifetimeCts.Token);
                if (Interlocked.Exchange(ref _renderCostRelayLogged, 1) == 0)
                {
                    _ = LogAsync(LogLevel.Info, "渲染开销上报链路已建立（Overlay→Main→Monitor，AC-95）");
                }

                break;

            case IpcMessageType.Warning when role == IpcRole.Overlay:
                // 子进程告警（AC-94 提权窗口/安全桌面等）：记日志 + 托盘提示（App 去重）
                var warning = e.Envelope.GetPayload<WarningReport>();
                if (warning is not null)
                {
                    _ = LogAsync(LogLevel.Warn, $"子进程告警：{warning.Message}", warning.ErrorCode);
                    _warnNotify?.Invoke(warning.ErrorCode, warning.Message);
                }

                break;
        }
    }

    /// <summary>尽力而为日志：2s 超时发往 Logging 管道，任何失败静默（不影响主流程）。</summary>
    private async Task LogAsync(LogLevel level, string message, string? errorCode = null, CancellationToken ct = default)
    {
        // AC-35 分层错误提示：致命级（ERROR/FATAL）且带错误码 → 同步通知托盘气泡层
        // （去重聚合在 UI 侧 ErrorNotifier；此处只转发，日志照常发送）。
        if ((level is LogLevel.Error or LogLevel.Fatal) && !string.IsNullOrEmpty(errorCode))
        {
            _fatalNotify?.Invoke(errorCode, message);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            await _logClient.SendAsync(IpcMessageType.LogEntry, new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Module = "main",
                ErrorCode = errorCode,
                Message = message,
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 日志通道超时：静默
        }
        catch (IOException)
        {
            // Logging 未就绪 / 已退出
        }
        catch
        {
            // 日志异常不影响主流程
        }
    }
}