using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Object1688.Shared.Config;
using Object1688.Shared.Perf;
using Object1688.Shared.Stats;

namespace Object1688.Monitor.Perf;

/// <summary>
/// 性能窗口（架构 §6.3 / AC-10/39/51/61/63/91/92）。
/// 实时展示 CPU/内存曲线（60s/5min 切换）、进程状态、检测事件流（环形 500）、触发统计；
/// 提供导出（CSV/TXT/JSON）与清除历史；关闭=隐藏（Monitor 常驻采集）；布局记忆（uiState.perfWin）。
/// 数据源为 <see cref="PerfSampler"/>（后台 1s 采集）与 <see cref="StatsTracker"/>（持久化统计）。
/// </summary>
public partial class PerfWindow : Window
{
    private readonly PerfSampler _sampler;
    private readonly MonitorFeed _feed;
    private readonly StatsTracker _stats;
    private readonly DispatcherTimer _refresh;
    private readonly Action<UiWindowLayout>? _onHidePersist;
    private string _window = "60s";
    private int _windowPoints = 60;

    /// <summary>当前窗口显示模式（60s / 5min）。</summary>
    public string TimeWindow => _window;

    /// <summary>初始化性能窗口并开始 UI 刷新。</summary>
    /// <param name="sampler">性能采样服务（已启动）。</param>
    /// <param name="feed">帧率曲线 + 最近一次大字回看数据（AC-68/AC-92）。</param>
    /// <param name="statsPath">stats.json 路径（供统计展示与导出）。</param>
    /// <param name="onHidePersist">窗口隐藏时布局写回回调（AC-61；由 App 负责落盘 uiState）。</param>
    public PerfWindow(PerfSampler sampler, MonitorFeed feed, string statsPath, Action<UiWindowLayout>? onHidePersist = null)
    {
        _sampler = sampler;
        _feed = feed;
        _stats = new StatsTracker(statsPath);
        _onHidePersist = onHidePersist;
        InitializeComponent();

        TimeWindowCombo.ItemsSource = new[] { "60s", "5min" };
        TimeWindowCombo.SelectedItem = "60s";

        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refresh.Tick += (_, _) => RefreshView();
        IsVisibleChanged += OnWindowVisibilityChanged;
        RefreshView();
    }

    /// <summary>窗口可见状态变化：仅可见时启动 1s UI 刷新（隐藏时停止，Monitor 自身不空耗）。</summary>
    private void OnWindowVisibilityChanged(object? sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _refresh.Start();
            RefreshView();
        }
        else
        {
            _refresh.Stop();
        }
    }

    /// <summary>将给定布局应用到窗口（窗口显示前由 App 调用；AC-61 布局记忆）。</summary>
    /// <param name="layout">uiState.perfWin 布局；null 用默认。</param>
    public void ApplyLayout(UiWindowLayout? layout)
    {
        if (layout is null)
        {
            return;
        }

        if (layout.Width is { } w && w > 0)
        {
            Width = w;
        }

        if (layout.Height is { } h && h > 0)
        {
            Height = h;
        }

        if (layout.X is { } x && layout.Y is { } y)
        {
            Left = x;
            Top = y;
        }

        if (!string.IsNullOrWhiteSpace(layout.TimeWindow))
        {
            TimeWindowCombo.SelectedItem = layout.TimeWindow;
        }
    }

    /// <summary>采集当前布局供持久化（关闭写回）。</summary>
    /// <returns>布局对象。</returns>
    public UiWindowLayout CaptureLayout() => new()
    {
        X = Left,
        Y = Top,
        Width = Width,
        Height = Height,
        TimeWindow = _window,
    };

    private void OnTimeWindowChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeWindowCombo.SelectedItem is not string value)
        {
            return;
        }

        _window = value;
        _windowPoints = value == "5min" ? 300 : 60;
        RefreshView();
    }

    private void RefreshView()
    {
        // 触发统计摘要
        var data = _stats.Data;
        SummaryText.Text = $"刷新 1s · 累计触发 {data.TotalTriggers} · 事件 {_sampler.Events.Count} 条 · 时间窗 {_window}";

        // 最近一次采样点按 tag 汇总为进程状态
        var latest = _sampler.Samples.Snapshot().GroupBy(s => s.ProcessTag)
            .Select(g => g.Last())
            .Select(s => new ProcessRow(s))
            .ToList();
        ProcessList.ItemsSource = latest;

        // 事件流（环形，倒序：最新在上）
        EventList.ItemsSource = _sampler.Events.Snapshot()
            .Reverse()
            .Take(50)
            .Select(e => $"[{e.Time.ToLocalTime():HH:mm:ss}] {e.Text}")
            .ToList();

        // 曲线
        var samples = _sampler.Samples.Snapshot().TakeLast(_windowPoints).ToList();
        DrawCurve(CpuCanvas, samples.Select(s => (double)s.CpuPercent).ToList(), "0", "%");
        DrawCurve(MemCanvas, samples.Select(s => (double)(s.WorkingSetBytes / 1024.0 / 1024.0)).ToList(), "0.0", "MB");

        // 帧率曲线（AC-68，Overlay 上报经 Main 转发）
        var fps = _feed.FrameRates.Snapshot().TakeLast(_windowPoints).Select(p => p.Fps).ToList();
        DrawCurve(FpsCanvas, fps, "0", "");

        // 规则触发排行（AC-63）：按计数 Top 5
        var rank = _stats.Data.PerRule
            .OrderByDescending(kv => kv.Value.Count)
            .Take(5)
            .Select(kv => (Label: kv.Key, Value: (double)kv.Value.Count))
            .ToList();
        DrawBars(RankCanvas, rank, new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)), labelEvery: 1);

        // 24h 时段分布（AC-63）
        var hours = Enumerable.Range(0, 24)
            .Select(h =>
            {
                var key = h.ToString(CultureInfo.InvariantCulture);
                var value = _stats.Data.ByHour.TryGetValue(key, out var v) ? v : 0;
                return (Label: key, Value: (double)value);
            })
            .ToList();
        DrawBars(HourCanvas, hours, new SolidColorBrush(Color.FromRgb(0xE5, 0x8A, 0x1E)), labelEvery: 6);

        // 最近一次大字回看（AC-92）
        var last = _feed.LastBanner;
        LastBannerText.Text = last is null
            ? "（暂无）"
            : $"[{last.Timestamp.ToLocalTime():HH:mm:ss}] {(last.RuleId is null ? "(手动/欢迎/测试)" : last.RuleId)}{Environment.NewLine}{string.Join(Environment.NewLine, last.Lines)}";

        // 渲染瞬时开销（AC-95）
        var costs = _feed.RenderCosts.Snapshot();
        RenderCostText.Text = costs.Count == 0
            ? "渲染开销：暂无"
            : $"渲染开销（AC-95）：CPU {costs[^1].CpuMs:F0}ms / 内存 +{costs[^1].MemMB:F1}MB（共 {costs.Count} 次）";

        StatusText.Text = $"样本 {samples.Count} 点（{_window} 窗）· 帧率 {fps.Count} 点";
    }

    private void DrawCurve(Canvas canvas, IReadOnlyList<double> values, string fmt, string unit)
    {
        canvas.Children.Clear();
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width <= 1 || height <= 1 || values.Count < 2)
        {
            return;
        }

        var max = values.Count == 0 ? 1 : Math.Max(values.Max(), 1);
        var points = new PointCollection();
        for (var i = 0; i < values.Count; i++)
        {
            var x = i / (double)(values.Count - 1) * width;
            var y = height - (values[i] / max) * (height - 8) - 4;
            points.Add(new Point(x, Math.Clamp(y, 0, height)));
        }

        var line = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)),
            StrokeThickness = 1.5,
        };
        canvas.Children.Add(line);

        // 最近值标注
        var last = values[^1];
        var label = new TextBlock
        {
            Text = last.ToString(fmt, CultureInfo.InvariantCulture) + unit,
            FontSize = 11,
            Foreground = Brushes.DarkSlateGray,
        };
        Canvas.SetLeft(label, Math.Max(0, width - 70));
        Canvas.SetTop(label, 2);
        canvas.Children.Add(label);
    }

    /// <summary>绘制柱状图（AC-63：触发排行 / 24h 分布）。<paramref name="labelEvery"/> 控制标签间隔（0=不标）。</summary>
    private static void DrawBars(Canvas canvas, IReadOnlyList<(string Label, double Value)> bars, Brush brush, int labelEvery)
    {
        canvas.Children.Clear();
        var width = canvas.ActualWidth;
        var height = canvas.ActualHeight;
        if (width <= 1 || height <= 1 || bars.Count == 0)
        {
            return;
        }

        var max = Math.Max(bars.Max(b => b.Value), 1);
        var slot = width / bars.Count;
        var barWidth = Math.Max(2, slot * 0.6);
        for (var i = 0; i < bars.Count; i++)
        {
            var barHeight = bars[i].Value / max * (height - 18);
            var rect = new Rectangle
            {
                Width = barWidth,
                Height = Math.Max(0, barHeight),
                Fill = brush,
            };
            Canvas.SetLeft(rect, i * slot + (slot - barWidth) / 2);
            Canvas.SetTop(rect, height - 16 - barHeight);
            canvas.Children.Add(rect);

            if (labelEvery > 0 && i % labelEvery == 0)
            {
                var label = new TextBlock { Text = bars[i].Label, FontSize = 9, Foreground = Brushes.DimGray };
                Canvas.SetLeft(label, i * slot + (slot - barWidth) / 2);
                Canvas.SetTop(label, height - 14);
                canvas.Children.Add(label);
            }
        }
    }

    private void OnExportClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var samples = _sampler.Samples.Snapshot().ToList();
            var events = _sampler.Events.Snapshot().ToList();
            var perf = PerfExporter.ExportPerfCsv(samples);
            var evCsv = PerfExporter.ExportEventsCsv(events);
            var statsJson = PerfExporter.ExportStatsJson(_stats.Data);
            StatusText.Text = $"已导出：{System.IO.Path.GetFileName(perf)}、{System.IO.Path.GetFileName(evCsv)}、{System.IO.Path.GetFileName(statsJson)}（log/exports）";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"导出失败：{ex.Message}";
        }
    }

    private async void OnClearHistoryClicked(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            this,
            "确认清除事件流统计与持久化触发计数？此操作不可恢复。",
            "清除历史",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        _sampler.Events.Clear();
        _stats.Reset();
        await Task.Yield();
        StatusText.Text = "历史已清除";
        RefreshView();
    }

    /// <summary>关闭 = 隐藏（Monitor 常驻采集；托盘可重新打开）。隐藏前回调布局写回。</summary>
    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        _onHidePersist?.Invoke(CaptureLayout());
        Hide();
    }

    private sealed record ProcessRow(PerfSample Sample)
    {
        public string Tag { get; } = Sample.ProcessTag;
        public string CpuText { get; } = Sample.CpuPercent.ToString("0.0", CultureInfo.InvariantCulture);
        public string MemText { get; } = (Sample.WorkingSetBytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
