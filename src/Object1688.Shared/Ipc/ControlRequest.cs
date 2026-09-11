namespace Object1688.Shared.Ipc;

/// <summary>
/// 外部脚本控制请求（F-76，单行 JSON）。
/// 命令名见 <see cref="ControlProtocol.Commands"/>；参数按命令可选。
/// </summary>
public sealed class ControlRequest
{
    /// <summary>协议版本（缺省 <see cref="ControlProtocol.Version"/>）。</summary>
    public int ProtocolVersion { get; init; } = ControlProtocol.Version;

    /// <summary>命令名（<see cref="ControlProtocol.Commands"/> 之一）。</summary>
    public required string Command { get; init; }

    /// <summary>banner 命令：文字内容（支持 <c>\n</c> 多行）。</summary>
    public string? Text { get; init; }

    /// <summary>banner 命令：目标屏（"primary" 或显示器索引号）。</summary>
    public string? TargetScreen { get; init; }

    /// <summary>banner 命令：字号（DIP；缺省用全局默认）。</summary>
    public double? FontSize { get; init; }

    /// <summary>banner 命令：保持秒数（缺省 4）。</summary>
    public double? HoldSeconds { get; init; }

    /// <summary>banner 命令：描边色（#RRGGBB；缺省用全局默认）。</summary>
    public string? OutlineColor { get; init; }

    /// <summary>banner 命令：描边宽度（DIP；缺省用全局默认）。</summary>
    public double? OutlineWidth { get; init; }

    /// <summary>banner 命令：描边方式（shadow/stroke；缺省用全局默认）。</summary>
    public string? OutlineMode { get; init; }
}
