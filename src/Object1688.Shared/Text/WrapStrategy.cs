namespace Object1688.Shared.Text;

/// <summary>
/// 大字换行策略（架构 §4.2 wrapStrategy）。
/// </summary>
public enum WrapStrategy
{
    /// <summary>按屏宽自动换行（默认）。</summary>
    Wrap,

    /// <summary>自动缩放字号适配单行（不换行）。</summary>
    Shrink,

    /// <summary>手动换行（文本中 \n 即换行，不自动折行）。</summary>
    Manual,
}