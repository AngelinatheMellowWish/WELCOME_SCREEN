using System.Text.Json;
using System.Text.Json.Serialization;

namespace Object1688.Shared.Stats;

/// <summary>
/// 触发统计持久化（架构 §6.4 / AC-47）：读写 <c>stats.json</c>。
/// 损坏自愈（AC-69 / 架构 §5.1）：文件缺失或不可解析时不阻断——原文件改名 <c>.corrupt</c> 留存，
/// 自动重建默认空结构返回，由调用方记录 CFG-W-1002。字段向前兼容（schemaVersion 演进）。
/// </summary>
public static class StatsFile
{
    /// <summary>当前 schema 版本。</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 读取统计文件（含自愈）。
    /// </summary>
    /// <param name="path">stats.json 完整路径。</param>
    /// <returns>解析结果（Healed=true 表示本次已自愈重建，调用方应记录 CFG-W-1002）。</returns>
    public static StatsLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            var fresh = new StatsData();
            TryWrite(fresh, path);
            return new StatsLoadResult(fresh, Healed: false);
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<StatsData>(json, Options);
            if (data is null)
            {
                return Heal(path);
            }

            return new StatsLoadResult(data, Healed: false);
        }
        catch (JsonException)
        {
            return Heal(path);
        }
        catch (IOException)
        {
            // 读取失败：不覆盖，返回默认（不阻断）；调用方可决定是否重试
            return new StatsLoadResult(new StatsData(), Healed: false);
        }
    }

    /// <summary>原子写统计文件（临时文件 + 替换）。</summary>
    /// <param name="data">统计数据。</param>
    /// <param name="path">stats.json 完整路径。</param>
    /// <returns>是否写入成功。</returns>
    public static bool TrySave(StatsData data, string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Options));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static StatsLoadResult Heal(string path)
    {
        try
        {
            var corrupt = path + ".corrupt";
            if (File.Exists(corrupt))
            {
                File.Delete(corrupt);
            }

            File.Move(path, corrupt); // 损坏原件备份留存排查
        }
        catch (IOException)
        {
            // 备份失败：继续重建（不阻断）
        }
        catch (UnauthorizedAccessException)
        {
            // 同上
        }

        var healed = new StatsData();
        TryWrite(healed, path);
        return new StatsLoadResult(healed, Healed: true);
    }

    private static void TryWrite(StatsData data, string path)
    {
        try
        {
            TrySave(data, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 重建落盘失败不影响本流程（下次触发再试）
        }
    }
}

/// <summary>统计加载结果。</summary>
/// <param name="Data">统计数据。</param>
/// <param name="Healed">本次是否发生损坏自愈（true 时应记录 CFG-W-1002）。</param>
public sealed record StatsLoadResult(StatsData Data, bool Healed);

/// <summary>触发统计数据结构（架构 §6.4）。</summary>
public sealed class StatsData
{
    /// <summary>Schema 版本。</summary>
    public int SchemaVersion { get; set; } = SchemaVersionDefault;

    /// <summary>总触发次数。</summary>
    public long TotalTriggers { get; set; }

    /// <summary>按规则计数与最近触发时间。</summary>
    public Dictionary<string, RuleStat> PerRule { get; set; } = new();

    /// <summary>24 小时分布（键 "0"~"23"）。</summary>
    public Dictionary<string, long> ByHour { get; set; } = new();

    /// <summary>最近更新时间（UTC）。</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>默认 schema 版本值（与 <see cref="StatsFile.SchemaVersion"/> 一致）。</summary>
    private const int SchemaVersionDefault = 1;
}

/// <summary>单规则统计。</summary>
public sealed class RuleStat
{
    /// <summary>触发次数。</summary>
    public long Count { get; set; }

    /// <summary>最近触发时间（UTC）。</summary>
    public DateTimeOffset? LastTriggerAt { get; set; }
}