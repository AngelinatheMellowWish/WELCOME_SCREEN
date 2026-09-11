using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Object1688.Shared;
using Object1688.Shared.Ipc;

namespace Object1688.Main;

/// <summary>
/// 单个子进程的监护单元：负责启动、心跳时间戳维护、指数退避重启调度与强制终止。
/// 退避序列为 1s → 2s → 4s → ...，上限 <see cref="IpcProtocol.RestartBackoffCap"/>（PRC-I-2003）。
/// 心跳由 <see cref="MarkHeartbeat"/> 更新，连续失联判定由监督循环按
/// <see cref="IpcProtocol.HeartbeatTimeout"/> 执行；心跳恢复正常后退避计数归零。
/// </summary>
internal sealed class ChildSupervisor
{
    private readonly string _exePath;
    private DateTimeOffset _nextTryAt = DateTimeOffset.MinValue;

    /// <summary>获取本监护单元对应的 IPC 角色（兼作子进程标识）。</summary>
    public IpcRole Role { get; }

    /// <summary>获取当前已启动的进程句柄；未启动或已回收时为 null。</summary>
    public Process? Process { get; private set; }

    /// <summary>获取最近一次心跳（或最近一次启动）的时间戳。</summary>
    public DateTimeOffset LastHeartbeat { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>获取当前连续重启尝试计数（心跳恢复后归零）。</summary>
    public int RestartAttempt { get; private set; }

    /// <summary>获取子进程是否正在运行中。</summary>
    public bool IsRunning => Process is { HasExited: false };

    /// <summary>获取是否已到下一次重试调度时刻。</summary>
    public bool RestartDue => DateTimeOffset.UtcNow >= _nextTryAt;

    /// <summary>初始化监护单元。</summary>
    /// <param name="role">子进程 IPC 角色。</param>
    /// <param name="exeName">子进程可执行文件名（位于主进程输出目录）。</param>
    public ChildSupervisor(IpcRole role, string exeName)
    {
        Role = role;
        _exePath = Path.Combine(AppContext.BaseDirectory, exeName);
    }

    /// <summary>刷新心跳时间戳并重置重启退避计数（IPC-W-7002 解除）。</summary>
    public void MarkHeartbeat()
    {
        LastHeartbeat = DateTimeOffset.UtcNow;
        RestartAttempt = 0;
    }

    /// <summary>
    /// 尝试启动子进程。
    /// </summary>
    /// <param name="failReason">启动失败时的原因描述；成功时为 null。</param>
    /// <returns>是否启动成功。</returns>
    public bool TryStart(out string? failReason)
    {
        failReason = null;
        try
        {
            var psi = new ProcessStartInfo(_exePath)
            {
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var process = Process.Start(psi);
            if (process is null)
            {
                failReason = "Process.Start 返回 null";
                return false;
            }

            Process = process;
            LastHeartbeat = DateTimeOffset.UtcNow;
            ScheduleNextTry(); // 用当前计数（0 起步 → 1s）
            RestartAttempt++;
            return true;
        }
        catch (Exception ex)
        {
            failReason = ex.Message;
            return false;
        }
    }

    /// <summary>按指数退避调度下一次启动尝试。</summary>
    public void ScheduleNextTry()
    {
        var seconds = Math.Min(Math.Pow(2, RestartAttempt), IpcProtocol.RestartBackoffCap.TotalSeconds);
        _nextTryAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(seconds);
    }

    /// <summary>强制终止子进程（含进程树），并回收句柄。</summary>
    public void Kill()
    {
        try
        {
            if (IsRunning)
            {
                Process!.Kill(entireProcessTree: true);
                Process!.WaitForExit(2000);
            }
        }
        catch (InvalidOperationException)
        {
            // 竞态：进程已退出
        }
        catch (Win32Exception)
        {
            // 杀进程权限/句柄异常，交由监督循环兜底
        }

        Process?.Dispose();
        Process = null;
    }

    /// <summary>标记子进程已按指示正常退出（主进程主动优雅退出路径），回收句柄。</summary>
    public void MarkGracefulExit()
    {
        Process?.Dispose();
        Process = null;
    }
}