// System.IO.MatchType 与配置 MatchType 同名（SDK 隐式 using），按测试工程既有别名模式消歧
using MatchType = Object1688.Shared.Config.MatchType;

namespace Object1688.Shared.Monitor;

/// <summary>
/// 单次监测快照中的候选目标（架构 §4.1/§4.4）。
/// 进程名或顶层窗口标题；窗口标题目标关联到所属进程 PID（AC-46 同窗口去重键）。
/// </summary>
public sealed record MatchTarget(
    MatchType Type,
    string Text,
    bool IsFullscreen,
    int ProcessId)
{
    /// <summary>创建进程名目标（§4.1 进程启动监测，进程名不含路径与命令行参数）。</summary>
    public static MatchTarget ForProcess(string processName, bool isFullscreen, int processId)
        => new(MatchType.Process, processName, isFullscreen, processId);

    /// <summary>创建窗口标题目标（§4.1 EnumWindows 顶层窗口；含所属进程 PID）。</summary>
    public static MatchTarget ForWindow(string windowTitle, bool isFullscreen, int processId)
        => new(MatchType.WindowTitle, windowTitle, isFullscreen, processId);
}