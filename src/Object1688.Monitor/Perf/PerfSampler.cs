using System.Diagnostics;
using Object1688.Shared.Perf;

namespace Object1688.Monitor.Perf;

/// <summary>
/// 性能采集服务（架构 §6.1/§6.2）：以 1s 周期采样五个 Object1688 进程（main/overlay/logging/monitor/configui）
/// 的 CPU%（TotalProcessorTime 差值，经 <see cref="ProcessProbe"/>）与内存（WorkingSet64），
/// 追加进采样环形缓冲（容量 300 = 5 分钟 @1s）；进程出现/消失生成事件流条目。
/// 采集在后台任务运行；窗口时间窗（60s/5min）由 UI 从同一缓冲切片。
/// </summary>
public sealed class PerfSampler : IAsyncDisposable
{
    /// <summary>采样环形缓冲容量（5 分钟 @1s）。</summary>
    public const int SampleCapacity = 300;

    private static readonly (string Tag, string Exe)[] Targets =
    {
        ("main", "Object1688.Main"),
        ("overlay", "Object1688.Overlay"),
        ("logging", "Object1688.Logging"),
        ("monitor", "Object1688.Monitor"),
        ("configui", "Object1688.ConfigUI"),
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<string, double> _prevCpuSeconds = new();
    private readonly HashSet<string> _seen = new();

    /// <summary>采样点环形缓冲（时间序，旧→新）。</summary>
    public RingBuffer<PerfSample> Samples { get; } = new(SampleCapacity);

    /// <summary>进程/检测事件流（环形 500）。</summary>
    public RingBuffer<EventItem> Events { get; } = new(500);

    /// <summary>上次采样 CPU 时间（供测试注入替代 Process 读取）。</summary>
    internal double? LastCpuOverrideSeconds { get; set; }

    /// <summary>启动周期采样循环（后台，直至 <see cref="DisposeAsync"/>）。</summary>
    public void Start()
    {
        _ = RunAsync(_cts.Token);
    }

    /// <summary>停止采样并释放资源。</summary>
    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>执行单轮采样（供测试直接调用）。</summary>
    public void SampleOnce(DateTimeOffset utcNow)
    {
        var present = new HashSet<string>();
        foreach (var (tag, exe) in Targets)
        {
            var processes = Process.GetProcessesByName(exe);
            present.Add(tag);
            if (processes.Length == 0)
            {
                continue; // 该进程未运行（如 ConfigUI 常驻关闭、或未拉起），不产生采样点
            }

            try
            {
                using var p = processes[0];
                var cpuSec = LastCpuOverrideSeconds ?? p.TotalProcessorTime.TotalSeconds;
                var mem = p.WorkingSet64;
                // 首次读到该进程累计 CPU 时间：无前值无法计算区间占用，置 0；其后以差值计算。
                var cpuPct = _prevCpuSeconds.TryGetValue(tag, out var prior)
                    ? ProcessProbe.ComputeCpuPercent(prior, cpuSec, 1.0)
                    : 0;
                _prevCpuSeconds[tag] = cpuSec;
                Samples.Append(new PerfSample(utcNow, tag, cpuPct, mem));

                if (!_seen.Contains(tag))
                {
                    _seen.Add(tag);
                    Events.Append(new EventItem(utcNow, $"{tag} 进程已运行"));
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 进程刚退出/权限不足：跳过本次采样（下一轮消失事件）
            }
        }

        // 消失检测：曾在场但本次不在 → 事件（不阻断，ConfigUI 可能本就不常驻）
        foreach (var gone in _seen.ToArray())
        {
            if (!present.Contains(gone))
            {
                _seen.Remove(gone);
                Events.Append(new EventItem(utcNow, $"{gone} 进程不在运行"));
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(ct))
            {
                SampleOnce(DateTimeOffset.UtcNow);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止路径
        }
    }
}
