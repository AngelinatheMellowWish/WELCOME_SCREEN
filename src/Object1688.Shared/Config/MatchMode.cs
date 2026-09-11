namespace Object1688.Shared.Config;

/// <summary>
/// 规则匹配模式（架构 §4.2 matchMode，F-10 三级匹配）。
/// 枚举名即配置文件/线上协议字符串（JSON 序列化启用枚举转字符串），禁止更改既有名称。
/// </summary>
public enum MatchMode
{
    /// <summary>完全匹配：目标文本与匹配值完全相等（区分大小写按规则 matchCaseSensitive）。</summary>
    Exact,

    /// <summary>包含匹配：目标文本包含匹配值即命中。</summary>
    Contains,

    /// <summary>
    /// 通配匹配：`*` 匹配任意多字符、`?` 匹配单字符；
    /// 实现转译为受控通配模式后匹配（架构 §4.2 注）。
    /// </summary>
    Wildcard,
}