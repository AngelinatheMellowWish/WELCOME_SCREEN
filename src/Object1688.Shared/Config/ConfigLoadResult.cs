using Object1688.Shared.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 配置加载结果：解析后的完整配置 + 加载/校验过程中产生的问题清单。
/// 无论配置文件缺失/损坏，均返回一个可用的 <see cref="Config"/>（降级为内置默认值），
/// 具体降级原因由调用方（Main 自检 / 日志）依据 <see cref="Issues"/> 决定是否记录。
/// </summary>
public sealed class ConfigLoadResult
{
    /// <summary>解析/降级后的有效配置（始终可用）。</summary>
    public required AppConfig Config { get; init; }

    /// <summary>加载过程问题清单（错误码 + 消息；空 = 全量成功）。</summary>
    public IReadOnlyList<ConfigIssue> Issues { get; init; } = [];
}