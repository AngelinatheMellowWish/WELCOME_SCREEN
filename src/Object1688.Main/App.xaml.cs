using System.Globalization;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using Object1688.Shared;
using Object1688.Shared.Cli;
using Object1688.Shared.Crash;
using Object1688.Shared.I18n;
using Object1688.Shared.Input;
using Object1688.Shared.Ipc;
using Object1688.Shared.Notify;
using WinForms = System.Windows.Forms;

namespace Object1688.Main;

/// <summary>
/// App 入口（架构 §8.7 命令行 / §2.4 生命周期）。
/// 职责：CLI 解析（--version 独立打印退出，AC-78）、单实例互斥、二次实例参数转发（AC-78）、
/// 托盘图标（退出入口）、会话结束事件（SessionEnding → 2s 落盘时限）。
/// </summary>
public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "Object1688.Main.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private MainCoordinator? _coordinator;
    private WinForms.NotifyIcon? _trayIcon;
    private GlobalHotkey? _hotkey;
    private readonly ErrorNotifier _errorNotifier = new();
    private readonly HashSet<string> _warnedCodes = new(StringComparer.Ordinal);
    private readonly WinForms.Timer _trayClickTimer = new() { Interval = 250 };
    private WinForms.ContextMenuStrip? _quickMenu;
    private WinForms.ToolStripMenuItem? _quickPauseItem;
    private DateTime _trayErrorUntilUtc;
    private string? _lastErrorCode;

    /// <summary>应用启动：安装崩溃护栏，解析 CLI，单实例校验，装配协调器并创建托盘。</summary>
    /// <param name="e">启动参数。</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CrashGuard.Install("main");

        var cli = CliParser.Parse(e.Args);
        if (!cli.IsValid)
        {
            NativeConsole.Print(CliParser.BuildUsageText());
            foreach (var error in cli.Errors)
            {
                NativeConsole.Print($"错误: {error}（{ErrorCodes.ConfigValidationFailed}）");
            }

            Shutdown(1);
            return;
        }

        if (cli.ShowVersion)
        {
            NativeConsole.Print($"Object1688 {GetVersion()}");
            Shutdown(0);
            return;
        }

        // 控制客户端（--control，F-77/AC-98）：独立短生命周期进程，不参与单实例/不启动 UI
        if (cli.ControlCommand is not null)
        {
            RunControlClient(cli);
            return;
        }

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            // 二次实例：把参数转发给既有 Main（AC-78），提示"已在运行"后退出
            _ = ForwardToPrimaryAsync(e.Args);
            return;
        }

        _coordinator = new MainCoordinator(cli, OnFatalError, OpenConfigWindow, ShowAbout, OnWarning);
        _coordinator.ConfigUpdated += OnConfigUpdated;
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _ = _coordinator.StartAsync();
        CreateTrayIcon();
        SetupHotkey();
        CheckAutostartHealth();
        MaybeShowFirstRunGuide();
    }

    /// <summary>应用退出：注销会话事件、释放托盘与单实例互斥。</summary>
    /// <param name="e">退出参数。</param>
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionEnding -= OnSessionEnding;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        if (_coordinator is not null)
        {
            _coordinator.ConfigUpdated -= OnConfigUpdated;
        }

        _hotkey?.Dispose();
        _hotkey = null;
        _trayIcon?.Dispose();
        _trayIcon = null;
        if (_singleInstanceMutex is not null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 非线程拥有者持有（进程退出场景），由 OS 回收
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    /// <summary>二次实例：连接既有 Main 的控制管道并转发参数（IPC-W-7004 失败提示）。</summary>
    private static async Task ForwardToPrimaryAsync(string[] args)
    {
        var succeeded = false;
        try
        {
            var handshake = new IpcHandshake
            {
                Role = IpcRole.Main,
                Pid = Environment.ProcessId,
                ExecutablePath = Environment.ProcessPath ?? string.Empty,
            };

            await using var client = new IpcPipeClient(IpcProtocol.MainControlPipe, handshake);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            _ = client.RunAsync(cts.Token);
            await client.SendAsync(IpcMessageType.RedirectArgs, args, cts.Token);
            succeeded = true;
        }
        catch
        {
            // 转发失败（IPC-W-7004）
        }

        NativeConsole.Print("Object1688 已在运行。");
        if (!succeeded)
        {
            NativeConsole.Print($"参数转发失败（{ErrorCodes.IpcRedirectFailed}）。");
        }

        Current.Dispatcher.Invoke(() => ((App)Current).Shutdown(succeeded ? 0 : 1));
    }

    /// <summary>会话结束（注销/关机）：取消默认处理并触发 2s 时限的优雅退出。</summary>
    private void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        e.Cancel = false;
        if (_coordinator is not null && !e.Cancel)
        {
            _ = _coordinator.RequestShutdownAsync(ShutdownReason.SessionEnding);
        }
    }

    /// <summary>电源事件（AC-41/AC-76）：睡眠/唤醒转发子进程（唤醒后 Overlay 延迟恢复监测）。</summary>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_coordinator is null)
        {
            return;
        }

        _ = e.Mode switch
        {
            PowerModes.Suspend => _coordinator.NotifySystemEventAsync(SystemEventKind.Suspend),
            PowerModes.Resume => _coordinator.NotifySystemEventAsync(SystemEventKind.Resume),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>会话切换（AC-50）：锁屏/断开结束播放队列且不补发，解锁/连接仅记录。</summary>
    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (_coordinator is null)
        {
            return;
        }

        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _ = _coordinator.NotifySystemEventAsync(SystemEventKind.Lock);
                break;

            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                _ = _coordinator.NotifySystemEventAsync(SystemEventKind.Unlock);
                break;
        }
    }

    /// <summary>子进程警告提示（F-42/NFR-04 扩展）：同码仅弹一次托盘气泡（如 OVL-W-3008 提权窗口无法覆盖）。</summary>
    private void OnWarning(string errorCode, string message)
    {
        _lastErrorCode = errorCode; // AC-93：供"复制最近错误码"
        lock (_warnedCodes)
        {
            if (!_warnedCodes.Add(errorCode))
            {
                return;
            }
        }

        Dispatcher.BeginInvoke(() => _trayIcon?.ShowBalloonTip(
            6000,
            $"{L("AppDisplayName")} ({errorCode})",
            message,
            WinForms.ToolTipIcon.Warning));
    }

    /// <summary>
    /// 致命错误通知（AC-35 致命层 + AC-84 去重聚合）：经 ErrorNotifier 判定，
    /// Show 弹含错误码气泡；Aggregate 弹"已发生 N 次"聚合气泡；Suppressed 静默。
    /// </summary>
    private void OnFatalError(string errorCode, string message)
    {
        _lastErrorCode = errorCode; // AC-93：供"复制最近错误码"
        var action = _errorNotifier.Decide(errorCode, DateTimeOffset.UtcNow);
        if (action.Kind == NotificationKind.Suppressed || _trayIcon is null)
        {
            return;
        }

        _trayErrorUntilUtc = DateTime.UtcNow.AddSeconds(60); // 托盘"错误"状态变体（AC-62）
        Dispatcher.BeginInvoke(() =>
        {
            UpdateTrayStatus();
            if (_trayIcon is null)
            {
                return;
            }

            var title = $"{L("ErrorBalloonTitle")} ({errorCode})";
            if (action.Kind == NotificationKind.Aggregate)
            {
                _trayIcon.ShowBalloonTip(
                    5000,
                    title,
                    L("ErrorAggregatedBody").Replace("{count}", action.CumulativeCount.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
                    WinForms.ToolTipIcon.Error);
            }
            else
            {
                _trayIcon.ShowBalloonTip(5000, title, message, WinForms.ToolTipIcon.Error);
            }
        });
    }

    /// <summary>托盘图标与菜单（F-50/AC-44/AC-09）：菜单文案走 i18n，语言切换后重建。</summary>
    private void CreateTrayIcon()
    {
        try
        {
            _trayIcon = new WinForms.NotifyIcon();
            _trayIcon.BalloonTipClicked += (_, _) => OpenLogFolder(); // AC-93：点击气泡打开日志

            // AC-44 托盘交互：左键快捷菜单 / 双击打开配置 / 悬停显示状态
            _trayClickTimer.Tick += (_, _) =>
            {
                _trayClickTimer.Stop();
                ShowQuickMenu();
            };
            _trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    _trayClickTimer.Stop();
                    _trayClickTimer.Start(); // 延迟区分单击/双击
                }
            };
            _trayIcon.MouseDoubleClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    _trayClickTimer.Stop();
                    OpenConfigWindow();
                }
            };

            ApplyLanguage();          // 先设置 Icon + Text
            _trayIcon.Visible = true; // 图标就绪后再显示
            TrayDiagnostic($"tray created: icon={(_trayIcon.Icon is null ? "NULL" : "ok")} visible={_trayIcon.Visible} text='{_trayIcon.Text}'");
        }
        catch (Exception ex)
        {
            TrayDiagnostic($"tray EXCEPTION: {ex}");
        }
    }

    /// <summary>托盘诊断日志（临时）：写入 %TEMP%\object1688-tray.log，便于排查图标未显示。</summary>
    private static void TrayDiagnostic(string message)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "object1688-tray.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 诊断写入失败忽略
        }
    }

    /// <summary>重建托盘右键菜单（i18n；语言切换后调用）。</summary>
    private void RebuildTrayMenu()
    {
        if (_trayIcon is null)
        {
            return;
        }

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(L("TraySettings"), null, (_, _) => OpenConfigWindow());
        menu.Items.Add(L("TrayManual"), null, (_, _) =>
        {
            if (_coordinator is not null)
            {
                _ = _coordinator.TriggerManualBigTextAsync();
            }
        });
        menu.Items.Add(L("TrayPerf"), null, (_, _) =>
        {
            if (_coordinator is not null)
            {
                _ = _coordinator.RequestPerfWindowAsync();
            }
        });
        menu.Items.Add(L("TrayLanguage"), null, (_, _) =>
        {
            if (_coordinator is not null)
            {
                _ = _coordinator.ToggleLanguageAsync();
            }

            ApplyLanguage();
        });
        menu.Items.Add(L("TrayAbout"), null, (_, _) => ShowAbout());
        menu.Items.Add(L("TrayDiagnostics"), null, (_, _) =>
        {
            if (_coordinator is not null)
            {
                _ = _coordinator.ExportDiagnosticsAsync();
            }
        });
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(L("TrayOpenLog"), null, (_, _) => OpenLogFolder());           // AC-93
        menu.Items.Add(L("TrayCopyLastError"), null, (_, _) => CopyLastErrorCode()); // AC-93
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(L("TrayExit"), null, (_, _) =>
        {
            if (_coordinator is not null)
            {
                _ = _coordinator.RequestShutdownAsync(ShutdownReason.UserExit);
            }
        });
        _trayIcon.ContextMenuStrip = menu;
        _quickMenu = null; // 快捷菜单按需重建（文案随语言）
    }

    /// <summary>应用界面语言（F-60/AC-09）：设置 CurrentUICulture 并刷新托盘文案与状态。</summary>
    private void ApplyLanguage()
    {
        var language = _coordinator?.Language;
        if (!string.IsNullOrWhiteSpace(language))
        {
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                // 非法语言值：沿用当前
            }
        }

        RebuildTrayMenu();
        UpdateTrayStatus();
    }

    /// <summary>打开日志文件夹（AC-93）：托盘"打开日志文件夹"与气泡点击。</summary>
    private void OpenLogFolder()
    {
        try
        {
            var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "log");
            if (!System.IO.Directory.Exists(dir))
            {
                NativeConsole.Print(L("LogFolderNotFound").Replace("{path}", dir, StringComparison.Ordinal));
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            NativeConsole.Print(ex.Message);
        }
    }

    /// <summary>复制最近错误码到剪贴板（AC-93）。</summary>
    private void CopyLastErrorCode()
    {
        if (string.IsNullOrEmpty(_lastErrorCode))
        {
            _trayIcon?.ShowBalloonTip(3000, L("AppDisplayName"), L("NoRecentError"), WinForms.ToolTipIcon.Info);
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_lastErrorCode);
            _trayIcon?.ShowBalloonTip(3000, L("AppDisplayName"), L("LastErrorCopied").Replace("{code}", _lastErrorCode, StringComparison.Ordinal), WinForms.ToolTipIcon.Info);
        }
        catch (Exception)
        {
            // 剪贴板被占用：静默
        }
    }

    /// <summary>i18n 取词（当前 UI 文化）。</summary>
    private static string L(string key) => LocalizedStrings.Get(key);

    /// <summary>左键快捷菜单（AC-44）：手动大字 / 暂停·恢复。</summary>
    private void ShowQuickMenu()
    {
        if (_quickMenu is null)
        {
            _quickMenu = new WinForms.ContextMenuStrip();
            _quickMenu.Items.Add(L("TrayManual"), null, (_, _) =>
            {
                if (_coordinator is not null)
                {
                    _ = _coordinator.TriggerManualBigTextAsync();
                }
            });
            _quickPauseItem = new WinForms.ToolStripMenuItem(L("TrayPause"));
            _quickPauseItem.Click += (_, _) =>
            {
                if (_coordinator is not null)
                {
                    _ = _coordinator.TogglePauseAsync();
                    UpdateTrayStatus();
                }
            };
            _quickMenu.Items.Add(_quickPauseItem);
        }

        if (_quickPauseItem is not null)
        {
            _quickPauseItem.Text = _coordinator?.IsPaused == true ? L("TrayResume") : L("TrayPause");
        }

        _quickMenu.Show(WinForms.Cursor.Position);
    }

    /// <summary>更新托盘图标与悬停状态（F-50/AC-44/AC-62/AC-77）：正常/暂停/勿扰/错误 × 深浅色。</summary>
    private void UpdateTrayStatus()
    {
        if (_trayIcon is null)
        {
            return;
        }

        var error = _trayErrorUntilUtc > DateTime.UtcNow;
        var paused = _coordinator?.IsPaused == true;
        var dnd = !error && !paused && _coordinator?.IsAutoSilenced() == true;
        var state = error ? "error" : paused ? "paused" : dnd ? "dnd" : "normal";
        var theme = DetectTrayTheme();

        var icon = LoadTrayIcon(state, theme) ?? LoadTrayIcon("normal", theme) ?? LoadFallbackTrayIcon();
        if (icon is not null)
        {
            _trayIcon.Icon = icon;
        }

        var statusKey = state switch
        {
            "error" => "StatusError",
            "paused" => "StatusPaused",
            "dnd" => "StatusDnd",
            _ => "StatusRunning",
        };
        _trayIcon.Text = L("TrayStatusFormat").Replace("{status}", L(statusKey), StringComparison.Ordinal);
    }

    /// <summary>从嵌入资源加载托盘图标（icons/tray_{state}_{theme}.ico）。</summary>
    private static System.Drawing.Icon? LoadTrayIcon(string state, string theme)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Object1688.Main;component/icons/tray_{state}_{theme}.ico", UriKind.Absolute);
            using var stream = GetResourceStream(uri)?.Stream;
            return stream is null ? null : new System.Drawing.Icon(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>兜底托盘图标：嵌入资源加载失败时回退用 exe 自带图标，保证托盘始终有图标可见。</summary>
    private static System.Drawing.Icon? LoadFallbackTrayIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) ? null : System.Drawing.Icon.ExtractAssociatedIcon(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>检测任务栏主题并返回图标字形主题（浅色任务栏→深色字形 dark；深色任务栏→浅色字形 light）。</summary>
    private static string DetectTrayTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            return value is int i && i == 1 ? "dark" : "light";
        }
        catch (Exception)
        {
            return "light";
        }
    }

    /// <summary>首次运行引导（F-13/AC-20）：首启经托盘气泡提示可从托盘配置；随后清除 firstRun 标志。</summary>
    private void MaybeShowFirstRunGuide()
    {
        if (_coordinator is null || !_coordinator.IsFirstRun)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _trayIcon?.ShowBalloonTip(
                6000,
                "Object1688",
                "Object1688 已在后台运行：右击托盘图标可手动触发大字、打开配置/性能窗口或退出（F-13）。",
                WinForms.ToolTipIcon.Info);
        });

        _ = _coordinator.AcknowledgeFirstRunAsync();
    }

    /// <summary>开机自启路径失效检测（F-24/AC-53）：注册项指向旧路径时托盘提示，引导在配置界面重新确认（不自动改写）。</summary>
    private void CheckAutostartHealth()
    {
        var invalid = _coordinator?.CheckAutostartInvalidPath();
        if (invalid is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => _trayIcon?.ShowBalloonTip(
            8000,
            "Object1688 开机自启",
            $"开机自启路径失效（指向 {invalid}）。请在配置窗口重新开启自启以修复（{ErrorCodes.AutostartPathInvalid}）。",
            WinForms.ToolTipIcon.Warning));
    }

    /// <summary>单例关于窗口：已打开则激活，否则新建。托盘"关于…"入口（AC-25）。</summary>
    private void ShowAbout()
    {
        Dispatcher.BeginInvoke(() =>
        {
            var existing = Windows.OfType<AboutWindow>().FirstOrDefault();
            if (existing is not null)
            {
                existing.Activate();
                return;
            }

            var about = new AboutWindow();
            about.Show();
        });
    }

    /// <summary>拉起配置窗口（托盘"设置…"与控制命令 config 共用）。</summary>
    private static void OpenConfigWindow()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = System.IO.Path.Combine(AppContext.BaseDirectory, "Object1688.ConfigUI.exe"),
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            NativeConsole.Print($"配置窗口启动失败：{ex.Message}");
        }
    }

    /// <summary>配置更新（含快捷键变更）：在 UI 线程重新注册全局热键（F-12/AC-71）。</summary>
    private void OnConfigUpdated() => Dispatcher.BeginInvoke(SetupHotkey);

    /// <summary>注册/重注册手动大字全局快捷键（F-12/F-20/AC-71）：冲突时记 PRC-W-2004 并托盘提示降级。</summary>
    private void SetupHotkey()
    {
        var shortcut = _coordinator?.ManualShortcut;
        _hotkey?.Dispose();
        _hotkey = null;

        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return; // 未启用快捷键
        }

        if (!HotkeyParser.TryParse(shortcut, out var spec))
        {
            NativeConsole.Print($"快捷键配置非法（{shortcut}），未注册（{ErrorCodes.HotkeyRegistrationFailed}）");
            return;
        }

        var hotkey = new GlobalHotkey();
        hotkey.Pressed += OnHotkeyPressed;
        if (hotkey.TryRegister(spec))
        {
            _hotkey = hotkey;
        }
        else
        {
            hotkey.Dispose();
            NativeConsole.Print($"快捷键 {spec.Normalized} 注册失败（可能被占用），已降级为无快捷键（{ErrorCodes.HotkeyRegistrationFailed}）");
            _trayIcon?.ShowBalloonTip(
                5000,
                "Object1688 快捷键",
                $"快捷键 {spec.Normalized} 被占用，注册失败（{ErrorCodes.HotkeyRegistrationFailed}）。可在配置窗口更换。",
                WinForms.ToolTipIcon.Warning);
        }
    }

    /// <summary>全局快捷键按下（F-07/AC-30）：正在显示大字 → 提前结束；否则触发手动大字。</summary>
    private void OnHotkeyPressed()
    {
        if (_coordinator is not null)
        {
            _ = _coordinator.OnManualHotkeyAsync();
        }
    }

    /// <summary>
    /// 控制客户端（--control，F-77/AC-98）：连接运行中的主实例控制管道，打印 JSON 响应后退出。
    /// 退出码：0=成功、2=命令执行失败、3=控制接口不可达（主实例未运行，IPC-E-7006）。
    /// </summary>
    private void RunControlClient(CliOptions cli)
    {
        var request = new ControlRequest
        {
            Command = cli.ControlCommand!,
            Text = cli.ControlText,
            TargetScreen = cli.ControlScreen,
        };

        var response = ControlClient.TrySendAsync(request).GetAwaiter().GetResult();
        if (response is null)
        {
            NativeConsole.Print($"Object1688 控制接口不可达（主实例未运行）：{ErrorCodes.IpcControlUnavailable}");
            Shutdown(3);
            return;
        }

        NativeConsole.Print(System.Text.Json.JsonSerializer.Serialize(response, IpcJson.Options));
        Shutdown(response.Ok ? 0 : 2);
    }

    private static string GetVersion()
    {
        var version = typeof(App).Assembly.GetName().Version;
        return version?.ToString(3) ?? "0.1.0";
    }
}