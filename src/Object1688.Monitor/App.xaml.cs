using System.Globalization;
using System.IO;
using System.Windows;
using Object1688.Monitor.Perf;
using Object1688.Shared.Config;
using Object1688.Shared.Crash;
using Object1688.Shared.Ipc;
using Object1688.Shared.Logging;
using Object1688.Shared.Perf;

namespace Object1688.Monitor;

/// <summary>
/// 监测进程入口（M1 骨架；M4-C 增性能采集与性能窗口）。
/// 职责：连接 Main 控制管道 → 握手 → 周期心跳 → 响应优雅退出（回执后退出码 0）；
/// 接收 ShowPerfWindow（托盘"性能窗口…"→ 打开/激活性能窗口，AC-10）。
/// 后台 1s 采集各进程 CPU/内存（PerfSampler）→ 环形缓冲供窗口展示与导出。
/// </summary>
public partial class App : Application
{
    private const string Module = "monitor";

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Object1688", "config.json");

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly MonitorFeed _feed = new();
    private PerfSampler? _sampler;
    private PerfWindow? _perfWindow;
    private int _lastBannerLogged;

    /// <summary>WPF 启动入口（UI 线程）。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CrashGuard.Install("monitor");

        // 本地化（AC-09）：按配置语言填充资源字典（XAML {DynamicResource}）
        var language = LoadLanguage();
        LocalizationService.Initialize(string.IsNullOrWhiteSpace(language) ? CultureInfo.CurrentUICulture : SafeCulture(language));

        _sampler = new PerfSampler();
        _sampler.Start();
        _ = Task.Run(() => RunWorkerAsync(_lifetimeCts.Token));
    }

    /// <summary>应用退出：停止采集并取消生命周期令牌。</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _lifetimeCts.Cancel();
        _sampler?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _perfWindow?.Close();
        base.OnExit(e);
    }

    /// <summary>UI 线程打开/激活性能窗口（含布局记忆 AC-61：读 uiState.perfWin 应用）。</summary>
    private void OpenPerfWindow()
    {
        if (_perfWindow is null)
        {
            var layout = LoadPerfLayout();
            _perfWindow = new PerfWindow(_sampler!, _feed, StatsPath(), PersistPerfLayout)
            {
                ShowInTaskbar = false,
            };
            _perfWindow.ApplyLayout(layout);
            _perfWindow.Closed += (_, _) => _perfWindow = null;
        }

        if (!_perfWindow.IsVisible)
        {
            _perfWindow.Show();
            _ = LogAsync(LogLevel.Info, "性能窗口已打开（AC-10/AC-67）", CancellationToken.None);
        }

        if (_perfWindow.WindowState == WindowState.Minimized)
        {
            _perfWindow.WindowState = WindowState.Normal;
        }

        _perfWindow.Activate();
    }

    private async Task RunWorkerAsync(CancellationToken ct)
    {
        var handshake = CreateHandshake();
        await LogAsync(LogLevel.Info, "Monitor 进程启动", ct);

        await using var client = new IpcPipeClient(IpcProtocol.MainControlPipe, handshake);
        client.MessageReceived += (_, envelope) =>
        {
            if (envelope.Type == IpcMessageType.Shutdown)
            {
                _ = HandleShutdownAsync(client);
            }
            else if (envelope.Type == IpcMessageType.ShowPerfWindow)
            {
                Dispatcher.Invoke(OpenPerfWindow);
            }
            else if (envelope.Type == IpcMessageType.FrameRate)
            {
                var rate = envelope.GetPayload<FrameRateReport>();
                if (rate is not null)
                {
                    _feed.AddFrameRate(rate.Fps, rate.Degraded);
                }
            }
            else if (envelope.Type == IpcMessageType.LastBanner)
            {
                var banner = envelope.GetPayload<LastBannerReport>();
                if (banner is not null)
                {
                    _feed.LastBanner = banner;
                    if (Interlocked.Exchange(ref _lastBannerLogged, 1) == 0)
                    {
                        _ = LogAsync(LogLevel.Info, "最近一次大字回看链路已建立（Main→Monitor，AC-92）", CancellationToken.None);
                    }
                }
            }
            else if (envelope.Type == IpcMessageType.RenderCost)
            {
                var cost = envelope.GetPayload<RenderCostReport>();
                if (cost is not null)
                {
                    _feed.AddRenderCost(cost.CpuMs, cost.MemMB);
                }
            }
        };

        // 接收循环后台运行（断线自动重连）
        _ = client.RunAsync(ct);

        // 心跳循环：HeartbeatInterval(2s) 上报存活，直至退出
        using var timer = new PeriodicTimer(IpcProtocol.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await client.SendAsync(IpcMessageType.Heartbeat, null, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出路径
        }
    }

    private async Task HandleShutdownAsync(IpcPipeClient client)
    {
        await LogAsync(LogLevel.Info, "收到退出指令，Monitor 进程退出", CancellationToken.None);
        try
        {
            await client.SendAsync(IpcMessageType.ShutdownAck, null, CancellationToken.None);
        }
        catch
        {
            // 回执失败不阻塞退出（Main 侧 3s 后会强制 Kill）
        }

        Dispatcher.Invoke(() => Shutdown(0));
    }

    private static IpcHandshake CreateHandshake() => new()
    {
        Role = IpcRole.Monitor,
        Pid = Environment.ProcessId,
        ExecutablePath = Environment.ProcessPath ?? string.Empty,
    };

    private static async Task LogAsync(LogLevel level, string message, CancellationToken ct, int timeoutMs = 2000)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await using var logClient = new IpcPipeClient(IpcProtocol.LoggingPipe, CreateHandshake());
            _ = logClient.RunAsync(cts.Token);
            await logClient.SendAsync(
                IpcMessageType.LogEntry,
                new LogEntry
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Level = level,
                    Module = Module,
                    Message = message,
                },
                cts.Token);
            await logClient.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // Logging 不可达（M1 最佳努力，不阻塞主流程）
        }
        catch (Exception)
        {
            // 同上
        }
    }

    /// <summary>读取配置语言（失败返回 null → 跟随系统）。</summary>
    private static string? LoadLanguage()
    {
        try
        {
            return ConfigLoader.Load(ConfigPath).Config.Language;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CultureInfo SafeCulture(string name)
    {
        try
        {
            return new CultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentUICulture;
        }
    }

    private static string StatsPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Object1688",
            "stats.json");

    /// <summary>读取 uiState.perfWin 布局（读配置失败/缺省时返回 null → 窗口用默认）。</summary>
    private static UiWindowLayout? LoadPerfLayout()
    {
        try
        {
            var result = ConfigLoader.Load(ConfigPath);
            return result.Config.UiState?.PerfWin;
        }
        catch
        {
            return null; // 配置不可用：窗口用默认布局
        }
    }

    /// <summary>窗口隐藏时写回 uiState.perfWin（AC-61 布局记忆）。</summary>
    /// <param name="layout">窗口布局。</param>
    private static void PersistPerfLayout(UiWindowLayout layout)
    {
        try
        {
            var current = ConfigLoader.Load(ConfigPath).Config;
            var merged = new AppConfig
            {
                SchemaVersion = current.SchemaVersion,
                Language = current.Language,
                Global = current.Global,
                Welcome = current.Welcome,
                Manual = current.Manual,
                Rules = current.Rules,
                Dnd = current.Dnd,
                Sound = current.Sound,
                Autostart = current.Autostart,
                FirstRun = current.FirstRun,
                UiState = current.UiState is null
                    ? new UiStateConfig { PerfWin = layout }
                    : new UiStateConfig { ConfigWin = current.UiState.ConfigWin, PerfWin = layout },
            };
            ConfigSaver.Save(ConfigPath, merged);
        }
        catch
        {
            // 布局写回失败不影响窗口隐藏（下次打开用默认）
        }
    }
}