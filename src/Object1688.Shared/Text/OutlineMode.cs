namespace Object1688.Shared.Text;

/// <summary>
/// 大字描边渲染方式（架构 §3.3 描边方案）。
/// <see cref="Shadow"/> = 方案 A（DropShadowEffect 柔光晕，模拟描边，开销低）；
/// <see cref="Stroke"/> = 方案 B（多向偏移实心描边，精确清晰，元素开销略高）。
/// 由配置 <c>global.outlineMode</c> / 规则级 <c>outlineMode</c> 选择，缺省 <see cref="Shadow"/>。
/// </summary>
public enum OutlineMode
{
    /// <summary>方案 A：柔光晕（单个 DropShadowEffect，边缘约 50% 覆盖的柔边）。</summary>
    Shadow,

    /// <summary>方案 B：精确描边（多向偏移实心复制，边缘实心清晰）。</summary>
    Stroke,
}
