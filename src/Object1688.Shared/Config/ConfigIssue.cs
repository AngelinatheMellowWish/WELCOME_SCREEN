namespace Object1688.Shared.Config;

/// <summary>
/// 配置加载/校验过程中的单条问题（对应 error_codes 表 CFG-* 各类）。
/// </summary>
public sealed class ConfigIssue
{
    /// <summary>错误码（如 CFG-V-1003），与 <see cref="Object1688.Shared.ErrorCodes"/> 常量一致。</summary>
    public required string ErrorCode { get; init; }

    /// <summary>人类可读消息（含上下文：规则 ID / 字段名）。</summary>
    public required string Message { get; init; }
}