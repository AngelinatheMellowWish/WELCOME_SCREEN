using System.Text.Json;

namespace Object1688.Shared.Ipc;

/// <summary>
/// 外部脚本控制响应（F-76，单行 JSON）。
/// 成功：<see cref="Ok"/> = true，<see cref="Data"/> 为命令结果（可空）；
/// 失败：<see cref="Ok"/> = false，<see cref="ErrorCode"/> / <see cref="Message"/> 说明原因。
/// </summary>
public sealed class ControlResponse
{
    /// <summary>是否成功。</summary>
    public required bool Ok { get; init; }

    /// <summary>失败错误码（成功时为 null）。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>失败/补充说明消息（成功时为 null）。</summary>
    public string? Message { get; init; }

    /// <summary>成功结果数据（命令自定义结构；可空）。</summary>
    public JsonElement? Data { get; init; }

    /// <summary>构造成功响应。</summary>
    public static ControlResponse Success(JsonElement? data = null) => new() { Ok = true, Data = data };

    /// <summary>构造失败响应。</summary>
    public static ControlResponse Failure(string errorCode, string message)
        => new() { Ok = false, ErrorCode = errorCode, Message = message };
}
