using Object1688.Shared;
using Object1688.Shared.Config;

namespace Object1688.Tests;

/// <summary>
/// SoundAssetValidator 测试（需求书 F-08 / AC-72 / CFG-V-1007）。
/// </summary>
public class SoundAssetValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "Object1688.SoundValidatorTests",
        Guid.NewGuid().ToString("N"));

    public SoundAssetValidatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // 测试清理不阻断结论
        }
    }

    private string WriteFile(string name, byte[] content = null!)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllBytes(p, content ?? new byte[100]);
        return p;
    }

    [Fact]
    public void Validate_EmptyPath_Fails()
    {
        var issues = SoundAssetValidator.Validate(string.Empty, _dir);
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigSoundInvalid);
    }

    [Fact]
    public void Validate_WrongExtension_Fails()
    {
        WriteFile("a.exe");
        var issues = SoundAssetValidator.Validate(Path.Combine(_dir, "a.exe"), _dir);
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigSoundInvalid);
    }

    [Fact]
    public void Validate_ValidWav_Succeeds()
    {
        WriteFile("ok.wav");
        var issues = SoundAssetValidator.Validate(Path.Combine(_dir, "ok.wav"), _dir);
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("beep.wav")]
    [InlineData("beep.mp3")]
    [InlineData("Beep.WAV")]
    public void Validate_AllowedExtensions_NoIssues(string name)
    {
        WriteFile(name);
        var issues = SoundAssetValidator.Validate(Path.Combine(_dir, name), _dir);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_MissingFile_Fails()
    {
        var issues = SoundAssetValidator.Validate(Path.Combine(_dir, "ghost.wav"), _dir);
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigSoundInvalid);
    }

    [Fact]
    public void Validate_RelativePath_Traversal_Fails()
    {
        // 相对路径逃出基准目录（.. 穿越）→ 拒绝
        var issues = SoundAssetValidator.Validate(Path.Combine("..", "..", "evil.wav"), _dir);
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigSoundInvalid);
    }

    [Fact]
    public void Validate_RelativePath_InsideBase_Ok()
    {
        var sub = Path.Combine(_dir, "sounds");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "ok.wav"), new byte[10]);
        var issues = SoundAssetValidator.Validate(Path.Combine("sounds", "ok.wav"), _dir);
        Assert.Empty(issues);
    }

    [Fact]
    public void Validate_AbsolutePath_OutsideBase_Allowed()
    {
        var outside = Path.Combine(Path.GetTempPath(), "Object1688.SoundOutside_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            var f = Path.Combine(outside, "sound.wav");
            File.WriteAllBytes(f, new byte[10]);
            var issues = SoundAssetValidator.Validate(f, _dir);
            Assert.Empty(issues); // 绝对路径允许任意可访问位置
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Validate_OverMaxBytes_Fails()
    {
        WriteFile("big.wav", new byte[300]);
        var issues = SoundAssetValidator.Validate(Path.Combine(_dir, "big.wav"), _dir, maxBytes: 100);
        Assert.Contains(issues, i => i.ErrorCode == ErrorCodes.ConfigSoundInvalid);
    }
}