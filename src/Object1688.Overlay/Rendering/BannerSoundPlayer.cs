using System.IO;
using System.Media;
using System.Windows.Media;

namespace Object1688.Overlay.Rendering;

/// <summary>
/// 大字提示音播放器（架构 §3.2 F-08：大字开始浮现时播放提示音，默认关）。
/// 音源：system（Windows 系统音）或 custom（用户自定义 wav/mp3 文件，音量 0~100）。
/// 提前结束（F-07）时同步停止（由队列服务在 EndCurrent 时调用 <see cref="Stop"/>）。
/// </summary>
public sealed class BannerSoundPlayer : IDisposable
{
    private MediaPlayer? _customPlayer;

    /// <summary>
    /// 播放提示音。
    /// </summary>
    /// <param name="source">"custom" = 自定义文件；其他 = 系统音。</param>
    /// <param name="customFile">自定义音源文件路径（source=custom 且文件存在时使用）。</param>
    /// <param name="volume">音量 0~100（自定义音源；系统音忽略）。</param>
    public void Play(string source, string? customFile, int volume = 60)
    {
        if (string.Equals(source, "custom", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(customFile)
            && File.Exists(customFile))
        {
            _customPlayer = new MediaPlayer
            {
                Volume = Math.Clamp(volume, 0, 100) / 100.0,
            };
            _customPlayer.Open(new Uri(Path.GetFullPath(customFile)));
            _customPlayer.Play();
            return;
        }

        // 默认系统音（F-08 默认关由调用方 PlaySound 开关裁决）
        SystemSounds.Asterisk.Play();
    }

    /// <summary>停止播放并释放自定义音源（提前结束 F-07 同步停止；幂等）。</summary>
    public void Stop()
    {
        _customPlayer?.Stop();
        _customPlayer?.Close();
        _customPlayer = null;
    }

    /// <summary>释放资源（停止播放并关闭自定义音源）。</summary>
    public void Dispose()
    {
        Stop();
    }
}