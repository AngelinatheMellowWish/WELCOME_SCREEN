namespace Object1688.Shared.Monitor;

/// <summary>
/// 单次监测快照中的进程条目（架构 §4.1 进程启动监测）。
/// <see cref="Name"/> 为进程名（Windows 取可执行模块名，含 ".exe"，如 "chrome.exe"）；
/// <see cref="ExecutablePath"/> 可空（权限不足时拿不到，用于 AC-45 同目录自身判定）。
/// </summary>
public sealed record MonitorProcessInfo(int Pid, string Name, string? ExecutablePath);

/// <summary>
/// 单次监测快照中的顶层窗口条目（架构 §4.1 EnumWindows 窗口标题监测）。
/// <see cref="OwnerPid"/> 为所属进程 PID（与进程条目关联，AC-46 同进程去重键）。
/// </summary>
public sealed record MonitorWindowInfo(int OwnerPid, string Title, bool IsFullscreen);

/// <summary>
/// 单次进程快照 + 窗口枚举结果（架构 §4.1，M3c 监测循环输入）。
/// 由 <see cref="IMonitorSnapshotProvider"/> 采集，监测循环将其转为 <see cref="MatchTarget"/> 列表。
/// </summary>
public sealed record MonitorSnapshot(
    IReadOnlyList<MonitorProcessInfo> Processes,
    IReadOnlyList<MonitorWindowInfo> Windows);

/// <summary>
/// 监测快照提供者（架构 §4.1，M3c）。
/// Overlay 侧真实实现为 Win32 封装（Process.GetProcesses + EnumWindows + 全屏检测）；
/// 测试侧以假实现驱动 <see cref="MonitorLoop"/> 单测。
/// 实现需线程安全：同一实例可能被监测循环单线程周期调用。
/// </summary>
public interface IMonitorSnapshotProvider
{
    /// <summary>采集一次进程快照与窗口枚举（访问受限进程可省略或置 ExecutablePath 为 null，不得抛致命异常）。</summary>
    MonitorSnapshot TakeSnapshot();
}