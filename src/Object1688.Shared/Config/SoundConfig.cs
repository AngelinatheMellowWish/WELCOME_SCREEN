namespace Object1688.Shared.Config;

/// <summary>
/// 大字提示音配置节（架构 §5.7 sound，F-08）。
/// 对应 config.json sound 对象。
/// </summary>
public sealed class SoundConfig
{
    /// <summary>提示音总开关。缺省 false（默认关，F-08）。</summary>
    public bool Enabled { get; init; }

    /// <summary>音源类型：system / custom。缺省 system。</summary>
    public string Source { get; init; } = "system";

    /// <summary>自定义音文件路径（Source=custom 时生效；CFG-V-1007 校验格式/大小/路径安全）。</summary>
    public string CustomPath { get; init; } = string.Empty;

    /// <summary>音量（0~100）。缺省 80。</summary>
    public int Volume { get; init; } = 80;
}