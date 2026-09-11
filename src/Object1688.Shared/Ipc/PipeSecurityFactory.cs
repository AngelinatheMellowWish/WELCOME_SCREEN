using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Object1688.Shared.Ipc;

/// <summary>
/// 命名管道安全描述符工厂（NFR-04/AC-70/AC-99）。
/// 统一构造「仅当前用户（+ 本地系统/管理员）」可访问的 <see cref="PipeSecurity"/>，
/// 供内部 MainControlPipe 与外部控制管道复用。
/// </summary>
public static class PipeSecurityFactory
{
    /// <summary>
    /// 构造仅当前用户可访问的管道安全描述符（不添加 World 拒绝 ACE —— DACL 仅含显式 Allow，
    /// 其余主体因缺少 Allow 自然被拒）。
    /// </summary>
    /// <remarks>
    /// 当前用户必须授予 FullControl，而非仅 ReadWrite：
    /// Windows 以 GENERIC_READ|GENERIC_WRITE（由 PipeDirection.InOut 展开，含
    /// SYNCHRONIZE/ReadAttributes/ReadEA/WriteEA/ReadPermissions 等）校验每个新实例句柄的
    /// 打开；若 DACL 授予范围不足，第二及后续实例的 CreateNamedPipe 会返回 ERROR_ACCESS_DENIED。
    /// 隔离语义不受影响——DACL 依旧只含当前用户/SYSTEM/管理员，其他登录会话无 Allow 自然被拒。
    /// </remarks>
    public static PipeSecurity CreateCurrentUserOnly()
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("无法获取当前用户 SID。");

        security.AddAccessRule(new PipeAccessRule(
            currentUser,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }
}
