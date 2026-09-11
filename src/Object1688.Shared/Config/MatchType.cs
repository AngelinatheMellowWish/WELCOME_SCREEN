namespace Object1688.Shared.Config;

/// <summary>
/// 规则匹配目标类型（架构 §4.2 matchType）。
/// <see cref="MatchType.Process"/>：按进程名匹配（如 chrome.exe，不含路径与命令行参数）；
/// <see cref="MatchType.WindowTitle"/>：按顶层窗口标题匹配（EnumWindows 枚举标题）。
/// </summary>
public enum MatchType
{
    /// <summary>按进程名匹配（如 chrome.exe）。</summary>
    Process,

    /// <summary>按顶层窗口标题匹配（EnumWindows 标题）。</summary>
    WindowTitle,
}