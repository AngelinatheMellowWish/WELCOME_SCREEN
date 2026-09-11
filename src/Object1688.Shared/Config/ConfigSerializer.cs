using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Object1688.Shared.Config;

/// <summary>
/// 配置序列化器（架构 §5.1/§5.5，F-21/AC-46）。
/// AppConfig → 缩进 JSON 文本；序列化契约与 config/config.default.json（唯一 schema 真源）逐字节风格一致：
/// camelCase 字段名 + 枚举转 camelCase 字符串（exact/process/windowTitle） + 非 ASCII 原样输出（UTF-8）+ 空依赖省略 null。
/// 与 <see cref="Ipc.IpcJson.Options"/>（IPC 紧凑载荷）互不影响，本类选项仅为磁盘文件/导出文件服务。
/// </summary>
public static class ConfigSerializer
{
    /// <summary>配置文件序列化选项（缩进 + camelCase 枚举字符串，供保存/导出复用）。</summary>
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>将完整配置序列化为缩进 JSON 文本（文件落盘/导出共用）。</summary>
    /// <param name="config">待保存的完整配置（各 required 节必须已构造）。</param>
    /// <returns>缩进 JSON 文本（UTF-8 无 BOM 语义，调用方写盘时用无 BOM 编码）。</returns>
    public static string Serialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return JsonSerializer.Serialize(config, Options);
    }
}