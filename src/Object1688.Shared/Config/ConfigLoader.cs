using System.Text.Json;
using System.Text.RegularExpressions;
using Object1688.Shared.Ipc;
using Object1688.Shared.Text;

namespace Object1688.Shared.Config;

/// <summary>
/// 配置加载器（架构 §5.1/§5.3/§5.7，F-22/AC-49）。
/// 职责：从用户配置路径（缺失→回退模板→回退内置默认）读取并解析 config.json，
/// 宽容加载（单条规则非法 → CFG-V-1003 记问题并跳过该规则，其余保留；整体损坏 → 回退默认），
/// 旧 schemaVersion 自动迁移（CFG-W-1008）、旧 displayText 兼容映射（开发规范 §9.3）、
/// §5.3 逐规则校验（匹配值/模式/时长范围/颜色/通配语法）。
/// </summary>
public static partial class ConfigLoader
{
    /// <summary>当前支持的配置 schema 版本（对外契约，见开发规范 §9.2）。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>延迟/保持时长防呆上限（秒，架构 §5.3「在上限内」）。</summary>
    public const double MaxDurationSeconds = 24 * 3600;

    /// <summary>发行模板默认路径（Main 首次启动/恢复出厂默认读取，§5.1/§5.5）。</summary>
    public static string DefaultTemplatePath => Path.Combine(AppContext.BaseDirectory, "config", "config.default.json");

    private static readonly Lazy<AppConfig> DefaultConfig = new(static () => BuildDefault());

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexColorRegex();

    /// <summary>
    /// 从文件加载配置：path 缺失 → 回退模板 → 回退内置默认（CFG-W-1002）；
    /// 读取失败 → CFG-E-1001 + 内置默认；JSON 解析/校验问题经 <see cref="Parse(string)"/> 处理。
    /// </summary>
    /// <param name="path">用户配置路径（config.json）；null/空白/不存在时按回退链处理。</param>
    /// <param name="templatePath">模板路径；null 时用 <see cref="DefaultTemplatePath"/>。</param>
    /// <returns>始终返回可用配置（默认值兜底）+ 问题清单。</returns>
    public static ConfigLoadResult Load(string? path, string? templatePath = null)
    {
        var issues = new List<ConfigIssue>();
        templatePath ??= DefaultTemplatePath;

        string? resolved = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (File.Exists(path))
            {
                resolved = path;
            }
            else
            {
                issues.Add(new ConfigIssue
                {
                    ErrorCode = ErrorCodes.ConfigDefaulted,
                    Message = $"配置文件缺失（{path}），回退模板/默认值",
                });
            }
        }

        if (resolved is null && !string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
        {
            resolved = templatePath;
        }

        if (resolved is null)
        {
            issues.Add(new ConfigIssue
            {
                ErrorCode = ErrorCodes.ConfigDefaulted,
                Message = "模板与配置文件均缺失，使用内置默认值",
            });
            return new ConfigLoadResult { Config = DefaultConfig.Value, Issues = issues };
        }

        string json;
        try
        {
            json = File.ReadAllText(resolved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new ConfigIssue
            {
                ErrorCode = ErrorCodes.ConfigReadFailed,
                Message = $"配置读取失败（{resolved}）：{ex.Message}",
            });
            return new ConfigLoadResult { Config = DefaultConfig.Value, Issues = issues };
        }

        return Parse(json, issues);
    }

    /// <summary>
    /// 从 JSON 文本解析配置（测试/未来导入场景 F-22）。
    /// 单条规则校验失败（CFG-V-1003）→ 跳过该规则保留其余；JSON 整体损坏 → 回退内置默认。
    /// </summary>
    /// <param name="json">config.json 文本。</param>
    /// <returns>始终返回可用配置（默认值兜底）+ 问题清单。</returns>
    public static ConfigLoadResult Parse(string json) => Parse(json, new List<ConfigIssue>());

    /// <summary>内置默认配置（缺省值与 config.default.json 模板一致；不含示例规则，首次初始化由 Main 复制模板）。</summary>
    public static AppConfig CreateDefault() => DefaultConfig.Value;

    private static ConfigLoadResult Parse(string json, List<ConfigIssue> issues)
    {
        AppConfigDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AppConfigDto>(json, IpcJson.Options);
        }
        catch (JsonException ex)
        {
            issues.Add(new ConfigIssue
            {
                ErrorCode = ErrorCodes.ConfigValidationFailed,
                Message = $"配置 JSON 解析失败：{ex.Message}",
            });
            return new ConfigLoadResult { Config = DefaultConfig.Value, Issues = issues };
        }

        if (dto is null)
        {
            issues.Add(new ConfigIssue
            {
                ErrorCode = ErrorCodes.ConfigValidationFailed,
                Message = "配置 JSON 解析结果为空",
            });
            return new ConfigLoadResult { Config = DefaultConfig.Value, Issues = issues };
        }

        // schema 版本治理（开发规范 §9.2）：旧版本自动迁移；未来版本拒绝解析
        var schemaVersion = dto.SchemaVersion ?? CurrentSchemaVersion;
        if (schemaVersion < CurrentSchemaVersion)
        {
            issues.Add(new ConfigIssue
            {
                ErrorCode = ErrorCodes.ConfigSchemaMigrated,
                Message = $"配置 schemaVersion {schemaVersion} 已自动升级迁移至 {CurrentSchemaVersion}",
            });
        }
        else if (schemaVersion > CurrentSchemaVersion)
        {
            issues.Add(new ConfigIssue
            {
                ErrorCode = ErrorCodes.ConfigValidationFailed,
                Message = $"配置 schemaVersion {schemaVersion} 高于当前支持版本 {CurrentSchemaVersion}，使用默认值",
            });
            return new ConfigLoadResult { Config = DefaultConfig.Value, Issues = issues };
        }

        // 规则逐条宽容映射：非法规则跳过并记 CFG-V-1003，其余保留
        var rules = new List<RuleConfig>();
        if (dto.Rules is { Count: > 0 })
        {
            foreach (var ruleDto in dto.Rules)
            {
                if (BuildRule(ruleDto, issues) is { } rule)
                {
                    rules.Add(rule);
                }
            }
        }

        var defaults = DefaultConfig.Value;
        var config = new AppConfig
        {
            SchemaVersion = CurrentSchemaVersion,
            Language = dto.Language,
            Global = dto.Global ?? defaults.Global,
            Welcome = dto.Welcome ?? defaults.Welcome,
            Manual = dto.Manual ?? defaults.Manual,
            Rules = rules,
            Dnd = dto.Dnd ?? defaults.Dnd,
            Sound = dto.Sound ?? defaults.Sound,
            Autostart = dto.Autostart ?? defaults.Autostart,
            FirstRun = dto.FirstRun ?? defaults.FirstRun,
            UiState = dto.UiState ?? defaults.UiState,
        };
        return new ConfigLoadResult { Config = config, Issues = issues };
    }

    /// <summary>单条规则映射 + §5.3 逐项校验；非法返回 null（已记 CFG-V-1003）。</summary>
    private static RuleConfig? BuildRule(RuleDto dto, List<ConfigIssue> issues)
    {
        void Fail(string detail) => issues.Add(new ConfigIssue
        {
            ErrorCode = ErrorCodes.ConfigValidationFailed,
            Message = $"规则 [{dto.RuleId ?? "(未命名)"}]：{detail}；已跳过该规则",
        });

        if (string.IsNullOrWhiteSpace(dto.RuleId))
        {
            Fail("ruleId 缺失");
            return null;
        }

        if (!Enum.TryParse<MatchType>(dto.MatchType, ignoreCase: true, out var matchType))
        {
            Fail($"matchType 非法（{dto.MatchType}）");
            return null;
        }

        // matchMode 缺省按 exact（架构 §4.2 注）
        var matchMode = MatchMode.Exact;
        if (!string.IsNullOrWhiteSpace(dto.MatchMode) &&
            !Enum.TryParse(dto.MatchMode, ignoreCase: true, out matchMode))
        {
            Fail($"matchMode 非法（{dto.MatchMode}）");
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.MatchValue))
        {
            Fail("匹配值 matchValue 不能为空");
            return null;
        }

        // 通配语法校验（§5.3）：仅允许普通文本 + * / ?；必须至少含一个通配符
        if (matchMode == MatchMode.Wildcard)
        {
            if (!WildcardPatternRegex().IsMatch(dto.MatchValue))
            {
                Fail($"通配符语法非法（仅支持 * 与 ? 通配符，不得含 [ ] ( ) {{ }} ^ $ + |）：{dto.MatchValue}");
                return null;
            }

            if (!dto.MatchValue.Contains('*') && !dto.MatchValue.Contains('?'))
            {
                Fail("通配模式（wildcard）下匹配值应至少含一个 * 或 ?");
                return null;
            }
        }

        // displayLines 或旧版单行 displayText（§4.2 注 / 开发规范 §9.3）
        IReadOnlyList<DisplayLine> displayLines;
        if (dto.DisplayLines is { Count: > 0 })
        {
            displayLines = dto.DisplayLines;
        }
        else if (!string.IsNullOrWhiteSpace(dto.DisplayText))
        {
            displayLines = new[]
            {
                new DisplayLine { Text = dto.DisplayText, FontSize = dto.FontSize ?? 0 },
            };
        }
        else
        {
            Fail("缺少 displayLines（或旧版 displayText）");
            return null;
        }

        var delaySeconds = dto.DelaySeconds ?? 2;
        var holdSeconds = dto.HoldSeconds ?? 4;
        if (delaySeconds < 0 || delaySeconds > MaxDurationSeconds)
        {
            Fail($"delaySeconds {delaySeconds} 超出合法范围 [0, {MaxDurationSeconds}]");
            return null;
        }

        if (holdSeconds < 0 || holdSeconds > MaxDurationSeconds)
        {
            Fail($"holdSeconds {holdSeconds} 超出合法范围 [0, {MaxDurationSeconds}]");
            return null;
        }

        // §5.7 合并语义：规则未显式配置的展示字段保留哨兵（空串 = 跟随全局默认），由 BannerAssembler 合并。
        var outlineColor = string.IsNullOrWhiteSpace(dto.OutlineColor) ? string.Empty : dto.OutlineColor;
        if (!string.IsNullOrWhiteSpace(outlineColor) && !HexColorRegex().IsMatch(outlineColor))
        {
            Fail($"描边颜色 outlineColor 非法（需 #RRGGBB 或 #AARRGGBB）：{dto.OutlineColor}");
            return null;
        }

        // 描边方式（架构 §3.3）：空串 = 跟随全局；非空必须为 shadow/stroke
        var outlineMode = string.IsNullOrWhiteSpace(dto.OutlineMode) ? string.Empty : dto.OutlineMode.Trim().ToLowerInvariant();
        if (outlineMode.Length > 0 && outlineMode is not ("shadow" or "stroke"))
        {
            Fail($"描边方式 outlineMode 非法（需 shadow / stroke）：{dto.OutlineMode}");
            return null;
        }

        var wrapStrategy = WrapStrategy.Wrap;
        if (!string.IsNullOrWhiteSpace(dto.WrapStrategy) &&
            !Enum.TryParse(dto.WrapStrategy, ignoreCase: true, out wrapStrategy))
        {
            Fail($"wrapStrategy 非法（{dto.WrapStrategy}）");
            return null;
        }

        return new RuleConfig
        {
            RuleId = dto.RuleId,
            MatchType = matchType,
            MatchValue = dto.MatchValue,
            MatchMode = matchMode,
            Enabled = dto.Enabled ?? true,
            MatchCaseSensitive = dto.MatchCaseSensitive ?? false,
            IncludeFullscreen = dto.IncludeFullscreen ?? true,
            // §5.7：未显式配置 → 空串哨兵（跟随全局），不得烘焙默认值
            TargetScreen = dto.TargetScreen ?? string.Empty,
            DisplayLines = displayLines,
            WrapStrategy = wrapStrategy,
            DelaySeconds = delaySeconds,
            HoldSeconds = holdSeconds,
            Position = dto.Position ?? string.Empty,
            OutlineColor = outlineColor,
            OutlineWidth = dto.OutlineWidth ?? -1,
            OutlineMode = outlineMode,
            MinIntervalSeconds = dto.MinIntervalSeconds,
            SoundEnabled = dto.SoundEnabled,
        };
    }

    [GeneratedRegex(@"^[^\[\](){}^$+|]+$")]
    private static partial Regex WildcardPatternRegex();

    /// <summary>内置默认配置：字段缺省与 config/default.json 模板一致（不含示例规则）。</summary>
    private static AppConfig BuildDefault() => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Language = null,
        Global = new GlobalConfig
        {
            DefaultFontSize = 96,
            DefaultPosition = "center",
            DefaultOutlineColor = "#000000",
            DefaultOutlineWidth = 0,
            OutlineMode = "shadow",
            TargetScreen = "primary",
            FontFamily = string.Empty,
            TimeFormat = "auto",
            MonitorPollIntervalMs = 1500,
            DedupeWindowSeconds = 10,
            MinTriggerIntervalSeconds = 2,
            LogLevel = "INFO",
            LogMaxSizeMB = 5,
            LogRetainCount = 10,
            DpiAwareness = "per-monitor-v2",
            RenderCostCpuWarnMs = 400,
            RenderCostMemWarnMB = 80,
        },
        Welcome = new WelcomeConfig
        {
            Enabled = true,
            DisplayLines = new[]
            {
                new DisplayLine { Text = "欢迎", FontSize = 120 },
                new DisplayLine { Text = "Welcome", FontSize = 48 },
            },
            DelaySeconds = 1,
            HoldSeconds = 4,
            WrapStrategy = WrapStrategy.Wrap,
        },
        Manual = new ManualConfig
        {
            Enabled = true,
            DisplayLines = new[]
            {
                new DisplayLine { Text = "Object1688", FontSize = 96 },
            },
            DelaySeconds = 0,
            HoldSeconds = 4,
            WrapStrategy = WrapStrategy.Wrap,
            Position = "center",
            FontSize = 96,
            Align = TextAlignment.Center,
            OutlineColor = string.Empty,
            OutlineWidth = -1,
            OutlineMode = string.Empty,
            TargetScreen = "primary",
            Shortcut = "Alt+F",
        },
        Rules = [],
        Dnd = new DndConfig
        {
            Paused = false,
            ScheduleEnabled = false,
            PresentationAutoSilence = false,
            Schedule = [],
        },
        Sound = new SoundConfig
        {
            Enabled = false,
            Source = "system",
            CustomPath = string.Empty,
            Volume = 80,
        },
        Autostart = true,
        FirstRun = true,
        UiState = new UiStateConfig
        {
            ConfigWin = new UiWindowLayout { Width = 900, Height = 640, Splitter = 280, Tab = "rules" },
            PerfWin = new UiWindowLayout { Width = 760, Height = 520, Splitter = 0, Tab = "overview", TimeWindow = "60s" },
        },
    };
}

/// <summary>config.json 顶层宽容 DTO（枚举以字符串承载，供逐项校验；缺失节为 null → 默认节）。</summary>
internal sealed class AppConfigDto
{
    public int? SchemaVersion { get; set; }

    public string? Language { get; set; }

    public GlobalConfig? Global { get; set; }

    public WelcomeConfig? Welcome { get; set; }

    public ManualConfig? Manual { get; set; }

    public List<RuleDto>? Rules { get; set; }

    public DndConfig? Dnd { get; set; }

    public SoundConfig? Sound { get; set; }

    public bool? Autostart { get; set; }

    public bool? FirstRun { get; set; }

    public UiStateConfig? UiState { get; set; }
}

/// <summary>单条规则宽容 DTO（枚举字段以字符串承载，missing → 缺省；支持旧版 displayText/fontSize）。</summary>
internal sealed class RuleDto
{
    public string? RuleId { get; set; }

    public string? MatchType { get; set; }

    public string? MatchValue { get; set; }

    public string? MatchMode { get; set; }

    public bool? Enabled { get; set; }

    public bool? MatchCaseSensitive { get; set; }

    public bool? IncludeFullscreen { get; set; }

    public string? TargetScreen { get; set; }

    public List<DisplayLine>? DisplayLines { get; set; }

    public string? DisplayText { get; set; }

    public double? FontSize { get; set; }

    public string? WrapStrategy { get; set; }

    public double? DelaySeconds { get; set; }

    public double? HoldSeconds { get; set; }

    public string? Position { get; set; }

    public string? OutlineColor { get; set; }

    public double? OutlineWidth { get; set; }

    public string? OutlineMode { get; set; }

    public double? MinIntervalSeconds { get; set; }

    public bool? SoundEnabled { get; set; }
}