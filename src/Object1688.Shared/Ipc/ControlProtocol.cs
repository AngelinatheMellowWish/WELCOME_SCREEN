namespace Object1688.Shared.Ipc;

/// <summary>
/// 外部脚本控制接口协议常量（F-76/F-77，AC-97/98/99）。
/// 通道：本地命名管道 <c>Object1688.control</c>（仅当前用户 ACL）。
/// 帧协议：单行 JSON 请求 / 单行 JSON 响应（UTF-8 无 BOM，\n 分隔）。
/// </summary>
public static class ControlProtocol
{
    /// <summary>控制管道名（服务端：Main；客户端：外部脚本 / <c>--control</c> 短生命周期进程）。</summary>
    public const string PipeName = "Object1688.control";

    /// <summary>控制协议版本（请求中携带，服务端校验）。</summary>
    public const int Version = 1;

    /// <summary>连接超时（主实例未运行时尽快失败，F-77）。</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>控制命令名（协议字符串，禁止更改既有名称）。</summary>
    public static class Commands
    {
        /// <summary>连通性探测。</summary>
        public const string Ping = "ping";

        /// <summary>运行状态查询。</summary>
        public const string Status = "status";

        /// <summary>立即显示任意大字（参数 text/targetScreen/...）。</summary>
        public const string Banner = "banner";

        /// <summary>触发已配置的手动大字（F-12）。</summary>
        public const string Manual = "manual";

        /// <summary>全局暂停（F-25）。</summary>
        public const string Pause = "pause";

        /// <summary>恢复（F-25）。</summary>
        public const string Resume = "resume";

        /// <summary>切换暂停/恢复（F-25）。</summary>
        public const string TogglePause = "toggle-pause";

        /// <summary>提前结束当前大字（F-07）。</summary>
        public const string End = "end";

        /// <summary>从磁盘重载配置并热生效。</summary>
        public const string Reload = "reload";

        /// <summary>打开配置窗口。</summary>
        public const string Config = "config";

        /// <summary>打开性能窗口。</summary>
        public const string Perf = "perf";

        /// <summary>打开关于窗口。</summary>
        public const string About = "about";

        /// <summary>导出一键诊断包（F-30 扩展/AC-83）。</summary>
        public const string Diagnostics = "diagnostics";

        /// <summary>优雅退出（F-74）。</summary>
        public const string Quit = "quit";
    }
}
