namespace Object1688.Shared;

/// <summary>
/// 错误码常量集（与 docs/error_codes[v1.1.0].md 表逐条一致，共 49 条）。
/// 硬性规则：一经定义不删除（可标记 [废弃]）；代码中字符串必须与本表完全一致；新增必须先登记文档。
/// </summary>
public static class ErrorCodes
{
    // ===== 2.1 配置系统（CFG）1000–1999 =====

    /// <summary>配置读取失败。</summary>
    public const string ConfigReadFailed = "CFG-E-1001";

    /// <summary>配置写入失败（原子写链路：锁超时/序列化回读校验失败/写盘失败，旧文件保留）。</summary>
    public const string ConfigWriteFailed = "CFG-E-1010";

    /// <summary>配置缺省，使用默认值。</summary>
    public const string ConfigDefaulted = "CFG-W-1002";

    /// <summary>配置校验失败。</summary>
    public const string ConfigValidationFailed = "CFG-V-1003";

    /// <summary>配置已保存/热生效。</summary>
    public const string ConfigSaved = "CFG-I-1004";

    /// <summary>导入规则 JSON 校验失败（格式/字段非法，F-22/AC-72）。</summary>
    public const string ConfigImportJsonInvalid = "CFG-V-1005";

    /// <summary>导入前自动备份当前配置失败（F-22）。</summary>
    public const string ConfigImportBackupFailed = "CFG-W-1006";

    /// <summary>自定义提示音校验失败（格式/大小/路径安全，F-08/AC-72）。</summary>
    public const string ConfigSoundInvalid = "CFG-V-1007";

    /// <summary>导入配置含旧 schemaVersion，已自动升级迁移（F-22/开发规范 §9.2）。</summary>
    public const string ConfigSchemaMigrated = "CFG-W-1008";

    /// <summary>{time} 占位符时间格式配置非法（F-04 扩展）。</summary>
    public const string ConfigTimeFormatInvalid = "CFG-V-1009";

    // ===== 2.2 进程管理（PRC）2000–2999 =====

    /// <summary>子进程启动失败。</summary>
    public const string ChildProcessStartFailed = "PRC-E-2001";

    /// <summary>子进程非正常退出。</summary>
    public const string ChildProcessAbnormalExit = "PRC-E-2002";

    /// <summary>子进程崩溃，已自动重启。</summary>
    public const string ChildProcessRestarted = "PRC-I-2003";

    /// <summary>全局热键注册失败（被其他程序占用，F-12/AC-71），进入无快捷键降级。</summary>
    public const string HotkeyRegistrationFailed = "PRC-W-2004";

    /// <summary>开机自启注册表项写入失败（F-24）。</summary>
    public const string AutostartWriteFailed = "PRC-W-2005";

    /// <summary>开机自启路径失效（注册项指向旧路径/非当前 exe，F-24/AC-53）。</summary>
    public const string AutostartPathInvalid = "PRC-W-2006";

    // ===== 2.3 渲染/叠加（OVL）3000–3999 =====

    /// <summary>叠加层创建失败。</summary>
    public const string OverlayCreateFailed = "OVL-E-3001";

    /// <summary>叠加层显示被系统抑制（独占全屏覆盖失败，已降级，F-10/AC-05）。</summary>
    public const string OverlaySuppressed = "OVL-W-3002";

    /// <summary>大字标题已显示。</summary>
    public const string OverlayBigTextShown = "OVL-I-3003";

    /// <summary>测试显示触发失败（目标屏不可用或 Overlay 未就绪，F-23/AC-59）。</summary>
    public const string OverlayTestTriggerFailed = "OVL-W-3004";

    /// <summary>嵌入字体资源加载失败（启动自检项，F-75/AC-82）。</summary>
    public const string OverlayFontLoadFailed = "OVL-E-3005";

    /// <summary>全局字体覆盖配置的字体不存在，已回退嵌入字体（F-05 扩展）。</summary>
    public const string OverlayFontFallback = "OVL-W-3006";

    /// <summary>大字触发瞬间渲染开销超阈值（CPU/内存峰值，NFR-02 扩展）。</summary>
    public const string OverlayRenderOverBudget = "OVL-W-3007";

    /// <summary>目标为安全桌面/提权窗口，无法覆盖（NFR-04 扩展）。</summary>
    public const string OverlaySecureDesktopBlocked = "OVL-W-3008";

    // ===== 2.4 日志系统（LOG）4000–4999 =====

    /// <summary>日志文件写入失败。</summary>
    public const string LogWriteFailed = "LOG-E-4001";

    /// <summary>日志轮转失败，继续使用当前文件。</summary>
    public const string LogRotationFailed = "LOG-W-4002";

    // ===== 2.5 崩溃转储（CRS）4000–4999（子类）=====

    /// <summary>崩溃转储生成失败。</summary>
    public const string CrashDumpFailed = "CRS-E-4003";

    /// <summary>崩溃转储已生成。</summary>
    public const string CrashDumpGenerated = "CRS-I-4004";

    /// <summary>crash 目录超上限清理失败（删除最旧转储失败，仅记日志，F-41/AC-52）。</summary>
    public const string CrashDumpCleanupFailed = "CRS-W-4005";

    // ===== 2.6 监测（MON）5000–5999 =====

    /// <summary>进程检测失败。</summary>
    public const string MonitorDetectionFailed = "MON-E-5001";

    /// <summary>规则未命中（匹配值找不到目标）。</summary>
    public const string MonitorRuleMiss = "MON-W-5002";

    /// <summary>规则命中，触发显示。</summary>
    public const string MonitorRuleHit = "MON-I-5003";

    /// <summary>规则已触发并入播放队列（F-10 串行队列）。</summary>
    public const string MonitorRuleQueued = "MON-I-5004";

    /// <summary>同规则命中被最小间隔抑制（规则级 minInterval，F-10 扩展/AC-46 相关）。</summary>
    public const string MonitorRuleSuppressed = "MON-W-5005";

    /// <summary>命中上报的规则不存在（配置可能已变更，TriggerEvent 已丢弃）。</summary>
    public const string MonitorRuleNotFound = "MON-W-5006";

    // ===== 2.7 文件 I/O（IO）6000–6999 =====

    /// <summary>导出文件写入失败。</summary>
    public const string IoExportWriteFailed = "IO-E-6001";

    /// <summary>文件被占用，稍后重试。</summary>
    public const string IoFileBusyRetry = "IO-W-6002";

    /// <summary>导出选中项无数据（空规则集，F-27）。</summary>
    public const string IoExportEmpty = "IO-W-6003";

    /// <summary>一键诊断包打包部分文件失败但已继续生成（F-30/AC-83）。</summary>
    public const string IoDiagPackagePartial = "IO-W-6004";

    // ===== 2.8 进程间通信（IPC）7000–7999 =====

    /// <summary>IPC 通信失败。</summary>
    public const string IpcCommunicationFailed = "IPC-E-7001";

    /// <summary>IPC 心跳超时（等待重连）。</summary>
    public const string IpcHeartbeatTimeout = "IPC-W-7002";

    /// <summary>管道连接方校验失败（非本程序进程尝试伪连，NFR-04/AC-70，已拒绝）。</summary>
    public const string IpcPeerValidationFailed = "IPC-E-7003";

    /// <summary>二次实例参数转发失败（提示"已在运行"，F-73/AC-78）。</summary>
    public const string IpcRedirectFailed = "IPC-W-7004";

    /// <summary>控制接口命令无效（未知命令或参数非法，F-76/AC-97）。</summary>
    public const string IpcControlCommandInvalid = "IPC-E-7005";

    /// <summary>控制接口连接失败（主实例未运行或控制管道不可达，F-77/AC-98）。</summary>
    public const string IpcControlUnavailable = "IPC-E-7006";

    // ===== 2.9 通用（GEN）9000 =====

    /// <summary>未分类错误（兜底）。</summary>
    public const string GenericUnhandled = "GEN-E-9001";

    /// <summary>程序崩溃（已生成转储）。</summary>
    public const string GenericCrash = "GEN-E-9002";

    /// <summary>i18n 资源键缺失，运行时回退英文并提示（AC-75）。</summary>
    public const string GenericI18nKeyMissing = "GEN-W-9003";

    /// <summary>启动自检某项失败，进入降级模式运行（F-75/AC-82）。</summary>
    public const string GenericDegradedMode = "GEN-W-9004";

    /// <summary>
    /// 全部错误码集合（固定顺序，供校验/测试使用）。
    /// </summary>
    public static readonly IReadOnlyList<string> AllCodes = new[]
    {
        ConfigReadFailed, ConfigDefaulted, ConfigValidationFailed, ConfigSaved,
        ConfigImportJsonInvalid, ConfigImportBackupFailed, ConfigSoundInvalid,
        ConfigSchemaMigrated, ConfigTimeFormatInvalid, ConfigWriteFailed,
        ChildProcessStartFailed, ChildProcessAbnormalExit, ChildProcessRestarted,
        HotkeyRegistrationFailed, AutostartWriteFailed, AutostartPathInvalid,
        OverlayCreateFailed, OverlaySuppressed, OverlayBigTextShown,
        OverlayTestTriggerFailed, OverlayFontLoadFailed, OverlayFontFallback,
        OverlayRenderOverBudget, OverlaySecureDesktopBlocked,
        LogWriteFailed, LogRotationFailed,
        CrashDumpFailed, CrashDumpGenerated, CrashDumpCleanupFailed,
        MonitorDetectionFailed, MonitorRuleMiss, MonitorRuleHit,
        MonitorRuleQueued, MonitorRuleSuppressed, MonitorRuleNotFound,
        IoExportWriteFailed, IoFileBusyRetry, IoExportEmpty, IoDiagPackagePartial,
        IpcCommunicationFailed, IpcHeartbeatTimeout, IpcPeerValidationFailed, IpcRedirectFailed,
        IpcControlCommandInvalid, IpcControlUnavailable,
        GenericUnhandled, GenericCrash, GenericI18nKeyMissing, GenericDegradedMode,
    };
}