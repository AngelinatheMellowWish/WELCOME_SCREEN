using Microsoft.Win32;
using Object1688.Shared.Config;

namespace Object1688.Tests;

/// <summary>
/// 自启路径校验测试（F-24/AC-53）。
/// 契约点：Enable 写入引号包裹路径 → GetRegisteredPath 去引号读回；IsRegisteredPathValid 仅在指向当前 exe 时为 true；
/// Disable 后未注册（返回 null / false）。使用独立测试键，避免污染真实 Run 键。
/// </summary>
public class AutostartRegistryTests : IDisposable
{
    private const string ValueName = "Object1688Test";
    private readonly string _keyPath = @"Software\Object1688.Tests\Autostart-" + Guid.NewGuid().ToString("N");

    public void Dispose() => Cleanup();

    private void Cleanup() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    [Fact]
    public void RegisteredPath_UnquotesAndValidatesAgainstCurrentExe()
    {
        var exe = @"C:\Program Files\Object1688\Object1688.Main.exe";
        AutostartRegistry.Enable(exe, _keyPath, ValueName);

        Assert.Equal(exe, AutostartRegistry.GetRegisteredPath(_keyPath, ValueName));
        Assert.True(AutostartRegistry.IsRegisteredPathValid(exe, _keyPath, ValueName));
        Assert.False(AutostartRegistry.IsRegisteredPathValid(@"D:\Elsewhere\Object1688.Main.exe", _keyPath, ValueName));

        AutostartRegistry.Disable(_keyPath, ValueName);
        Assert.Null(AutostartRegistry.GetRegisteredPath(_keyPath, ValueName));
        Assert.False(AutostartRegistry.IsRegisteredPathValid(exe, _keyPath, ValueName));
    }

    [Fact]
    public void GetRegisteredPath_MissingKey_ReturnsNull()
        => Assert.Null(AutostartRegistry.GetRegisteredPath(_keyPath, ValueName));
}
