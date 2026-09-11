using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Object1688.Shared.Ipc;

/// <summary>
/// IPC 统一 JSON 序列化配置（camelCase + 枚举转字符串 + 紧凑输出 + 非 ASCII 原样输出）。
/// M2 起启用 <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>：载荷经命名管道直连，
/// 不会进入 HTML 上下文，中文按 UTF-8 原样书写避免 \uXXXX 膨胀（消息可读、体积小 6 倍）。
/// </summary>
public static class IpcJson
{
    /// <summary>系统级 JSON 选项（惰性初始化，进程内唯一实例）。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };
}