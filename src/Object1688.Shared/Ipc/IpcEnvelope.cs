using System.Text.Json;

namespace Object1688.Shared.Ipc;

/// <summary>
/// IPC 消息信封（架构 §2.3 协议定版）。
/// 帧协议：单行 JSON（UTF-8 无 BOM，换行符分隔）。
/// </summary>
public sealed class IpcEnvelope
{
    /// <summary>协议版本（IpcProtocol.Version）。</summary>
    public required int ProtocolVersion { get; init; }

    /// <summary>消息类型。</summary>
    public required IpcMessageType Type { get; init; }

    /// <summary>发送方产生的单调递增序号（去重/排序）。</summary>
    public required long Seq { get; init; }

    /// <summary>发送时间（UTC，RFC 3339）。</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>载荷（JSON 元素，可空）。</summary>
    public JsonElement? Payload { get; init; }

    /// <summary>
    /// 创建信封（时间戳取当前 UTC）。
    /// </summary>
    public static IpcEnvelope Create(IpcMessageType type, long seq, JsonElement? payload = null)
        => new()
        {
            ProtocolVersion = IpcProtocol.Version,
            Type = type,
            Seq = seq,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = payload,
        };

    /// <summary>
    /// 将载荷反序列化为指定类型（Payload 为空或类型不匹配时返回 null）。
    /// </summary>
    public T? GetPayload<T>() => Payload is { } p ? p.Deserialize<T>(IpcJson.Options) : default;
}