using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Application = System.Windows.Application;
using Object1688.Shared.Config;
using Object1688.Shared.Crash;
using Object1688.Shared.I18n;
using Object1688.Shared.Ipc;
using Object1688.Shared.Logging;

namespace Object1688.ConfigUI;

/// <summary>
/// 配置界面进程入口（M2 骨架；M4b 补齐主窗口）。
/// 职责：单实例互斥（二次实例抬升既有窗口即退出）→ 连接 Main 控制管道 → 握手 → 周期心跳 →
/// 响应优雅退出（回执后退出码 0）；主窗口保存/测试显示经控制管道上报 Main（ConfigChanged/TestPlay）。
/// </summary>
public partial class App : Application
{
    private const string Module = "configui";

    /// <summary>单实例互斥名（二次启动时抬升既有窗口，不重复拉起进程）。</summary>
    private const string SingleInstanceMutexName = "Object1688.ConfigUI.SingleInstance";

    /// <summary>主窗口标题资源键（二次实例按标题查找抬窗；与 MainWindow.xaml Title 一致）。</summary>
    private const string MainWindowTitleKey = "ConfigWindowTitle";

    private readonly CancellationTokenSource _lifetimeCts = new();

    private Mutex? _singleInstanceMutex;
    private IpcPipeClient? _controlClient;
    private MainWindow? _mainWindow;

    /// <summary>WPF 启动入口（UI 线程）。</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CrashGuard.Install("configui");

        // 本地化（AC-09）：按配置语言填充资源字典（XAML {DynamicResource}）
        var language = LoadLanguage();
        LocalizationService.Initialize(string.IsNullOrWhiteSpace(language) ? CultureInfo.CurrentUICulture : SafeCulture(language));

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            BringExistingWindowToFront();
            Shutdown(0);
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        _ = Task.Run(() => RunWorkerAsync(_lifetimeCts.Token));
    }

    /// <summary>应用退出：取消生命周期令牌（放行窗口真正关闭）。</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _lifetimeCts.Cancel();
        if (_mainWindow is not null)
        {
            _mainWindow._lifetimeCancelled = true;
        }

        _controlClient?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }

    /// <summary>
    /// 经控制管道向 Main 发送消息（ConfigChanged 配置热生效 / TestPlay 测试显示）；
    /// 未连接时 SendAsync 内部排队直至握手完成。
    /// </summary>
    /// <param name="type">消息类型。</param>
    /// <param name="payload">载荷（AppConfig / BannerRequest）。</param>
    /// <param name="timeout">发送超时（超出抛 <see cref="TimeoutException"/>）。</param>
    internal async Task SendToMainAsync(IpcMessageType type, object? payload, TimeSpan timeout)
    {
        if (_controlClient is null)
        {
            throw new TimeoutException("控制管道客户端尚未就绪");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        cts.CancelAfter(timeout);
        await _controlClient.SendAsync(type, payload, cts.Token).ConfigureAwait(false);
    }

    private async Task RunWorkerAsync(CancellationToken ct)
    {
        var handshake = CreateHandshake();
        await LogAsync(LogLevel.Info, "ConfigUI 进程启动（配置窗口）", ct);

        var client = new IpcPipeClient(IpcProtocol.MainControlPipe, handshake);
        _controlClient = client;
        client.MessageReceived += (_, envelope) =>
        {
            if (envelope.Type == IpcMessageType.Shutdown)
            {
                _ = HandleShutdownAsync(client);
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
        await LogAsync(LogLevel.Info, "收到退出指令，ConfigUI 进程退出", CancellationToken.None);
        try
        {
            await client.SendAsync(IpcMessageType.ShutdownAck, null, CancellationToken.None);
        }
        catch
        {
            // 回执失败不阻塞退出（Main 侧 3s 后会强制 Kill）
        }

        if (_mainWindow is not null)
        {
            _mainWindow._lifetimeCancelled = true;
        }

        Dispatcher.Invoke(() => Shutdown(0));
    }

    /// <summary>二次实例：按窗口标题查找既有主窗口并抬升前台，然后退出本实例。</summary>
    private static void BringExistingWindowToFront()
    {
        var hwnd = FindWindowW(null, LocalizedStrings.Get(MainWindowTitleKey));
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
    }

    /// <summary>读取配置语言（失败返回 null → 跟随系统）。</summary>
    private static string? LoadLanguage()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Object1688", "config.json");
            return ConfigLoader.Load(path).Config.Language;
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

    private static IpcHandshake CreateHandshake() => new()
    {
        Role = IpcRole.ConfigUI,
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

    // ===== user32（二次实例抬窗）=====
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string? className, string windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}