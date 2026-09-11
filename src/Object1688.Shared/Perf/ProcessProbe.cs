using System.Diagnostics;

namespace Object1688.Shared.Perf;

/// <summary>
/// 进程 CPU/内存占用探针（架构 §6.1）：以 <c>TotalProcessorTime</c> 差值除以核数计算 CPU 百分比
/// （规避 PerformanceCounter 的权限/依赖问题），内存取 <c>WorkingSet64</c>。
/// 纯计算逻辑与系统读取分离，差值计算可独立单测。
/// </summary>
public static class ProcessProbe
{
    /// <summary>逻辑处理器核数（多线程进程 CPU% 理论上限）。</summary>
    public static int ProcessorCount { get; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// 由两次采样时刻的累计 CPU 时间计算该区间 CPU 占用百分比。
    /// 语义：**100% = 一个满核**（与任务管理器进程视图一致）；多线程进程可超 100%，
    /// 上限钳制为核数×100（全核满载）。
    /// </summary>
    /// <param name="priorTotalSeconds">上次采样累计 CPU 秒数。</param>
    /// <param name="currentTotalSeconds">本次采样累计 CPU 秒数。</param>
    /// <param name="elapsedSeconds">两次采样墙钟间隔（秒，&gt;0）。</param>
    /// <returns>CPU 占用百分比（0 ~ 核数×100；非法输入返回 0）。</returns>
    public static double ComputeCpuPercent(double priorTotalSeconds, double currentTotalSeconds, double elapsedSeconds)
    {
        if (elapsedSeconds <= 0 || currentTotalSeconds < priorTotalSeconds)
        {
            return 0;
        }

        var cpuSeconds = currentTotalSeconds - priorTotalSeconds;
        var pct = (cpuSeconds / elapsedSeconds) * 100.0;
        return Math.Clamp(pct, 0, 100.0 * ProcessorCount);
    }

    /// <summary>读取当前进程累计 CPU 时间（秒）。</summary>
    public static double ReadTotalCpuSeconds(Process process) => process.TotalProcessorTime.TotalSeconds;
}
