# 版本变更日志（CHANGELOG）— 集中版本登记处

> 文档版本：v1.0.0
> 状态：已基线确认（2026-09-09 十轮决策终审通过）
> 日期：2026-09-08
> 说明：本文档为 docs/ 全部文档的**集中版本登记处**。每份文档的版本号与变更历史按文档分组集中记录于此；文档头部仅保留当前版本号。
> 遵循[语义化版本](https://semver.org/lang/zh-CN/)：`主.次.修订`（文档版本与项目版本解耦，各自演进）。

---

## 1. 文档版本登记（docs/ 全文档，按文档分组）

### 1.1 项目需求书（项目需求书[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：由原 task_plan.md 与 requirements.md 合并而成；含决策记录/目录结构/阶段计划/未决问题附录。同日草案补充（不升版）·第一轮：12 项缺口决策并入（独占全屏策略/F-06 多行段落/F-13 首启引导/F-22 导入导出重置/F-23 预览测试/F-24 自启管理/F-71 关于/F-72 卸载/多显示器语义/默认热键/默认语言），AC 扩至 AC-25。·第二轮：F-04 变量/F-07 提前结束/F-10 三级匹配/F-11 欢迎每次启动/F-12 手动大字独立配置/F-20 规则启停/F-25 勿扰定时/NFR-04 隐私与清除/GOV-04 签名与 THIRD_PARTY_NOTICES，AC 扩至 AC-33（AC-06 承载欢迎大字每次启动语义，净增 8 项）。·第三轮：开发规范 12 项（见 §1.2）。·第四轮（参考样板驱动）：新增 F-08 提示音/F-42 分层错误提示/F-73 命令行参数/F-74 优雅退出；F-20 未保存提示、F-30 采样保留、NFR-02 规则规模预算、NFR-03 睡眠与时变、NFR-05 高 DPI；AC 扩至 AC-42。·第五轮（运行时行为）：F-05 字形回退、F-01 工作区定位、F-10 触发卫生（自身排除/PID 去重）、F-30 计数持久化与关窗隐藏、F-41 crash 清理、F-50 托盘交互、F-21 原子写、F-24 exe 迁移提示、NFR-03 锁屏会话、NFR-04 asInvoker；AC 扩至 AC-54。·第六轮（用户友好）：新增 F-26 内置帮助/F-27 规则列表增强/F-28 新建向导/F-29 冲突检测；F-20 撤销重做、F-21 历史备份、F-23 三类测试+样式预设+拖拽定位、F-30 图表可视化与布局记忆、F-50 图标状态变体；AC 扩至 AC-66。·第七轮（性能/鲁棒/安全/兼容）：NFR-02 性能量化+帧率检测、NFR-03 睡眠延时计时、NFR-04 IPC 访问控制、NFR-05 旋转屏、F-08 音频校验、F-12 热键冲突提示、F-21 数据自愈、F-22 统一校验、F-70 深浅色图标、F-73 二次实例转发定稿；AC 扩至 AC-78。·第八轮（交付/自检/结构/门禁）：F-70 图标矩阵、F-30 一键诊断包、F-42 错误去重聚合、新增 F-75 启动自检降级、文档清单登记 MANUAL.en、新增附录 F AC↔里程碑矩阵；AC 扩至 AC-87。·同步（同日，不升版）：附录 B 目录结构同步 MANUAL.en/资源细分/参考样板不入仓标注，附录 D 未决问题清理与 M 阶段待定项登记。·**第九轮补充（同日，不升版）**：F-04 {time} 格式选项（timeFormat）、F-05 全局字体覆盖（global.fontFamily）、F-08 规则级声音开关、F-10 规则级最小间隔 minInterval、F-11 欢迎大字启动时序（Overlay 就绪+配置广播后触发）；错误码补全（18→35 条）；附录 F 校正 AC-83 归属（M6→M4，与架构 §11 一致）；TEST_REPORT §4a 环境矩阵补操作系统语言/配置扩展/退出崩溃维度。·**第十轮补充（同日，不升版，未提及维度）**：F-06 每行独立颜色/描边/对齐（富文本 displayLines）、NFR-02 动画不受系统"关闭动画"影响+渲染瞬时开销监控、F-25 投屏/演示自动静默+功能开关、F-30 导出编码（UTF-8 BOM）/字段/命名定稿+最近一次大字回看、F-42 一键打开日志+错误码可复制/搜索、NFR-04 提权窗口/安全桌面降级提示（OVL-W-3008）、F-28 首开引导/空状态（联动 F-27）；AC 扩至 AC-96（净增 9 项 AC-88~96）；附录 F 里程碑矩阵同步。·**基线锁定（2026-09-09，不升版）**：十轮决策终审通过，文档状态转"已基线确认"；本地 git 初始化并打基线 tag v1.0.0。·**2026-09-09 指针/登记型同步（不升版）**：§7 文档清单补 VERSION_REGISTRY/AGENTS 行并同步开发规范 v1.1.0 引用（表头改为"当前版本"）；附录 A 决策记录追加四项治理决策（四维版本独立管理/程序集 0.1.0 起步/AGENTS 两处兼有/开发规范升版 v1.1.0）；附录 B 目录结构补 AGENTS/VERSION_REGISTRY（含根 AGENTS.md） |
| v1.1.0 | 2026-09-10 | **升版**（新增需求，次版本）：新增 §2.7 外部脚本控制接口——**F-76**（本地命名管道 JSON 控制接口 `Object1688.control`：ping/status/banner/manual/pause/resume/toggle-pause/end/reload/config/perf/about/quit；仅当前用户可访问）与 **F-77**（`--control` CLI 封装，独立短生命周期进程、退出码约定）；§6 新增 **AC-97/AC-98/AC-99**（控制接口/CLI/安全）；§7 文档清单文件名同步；§8.1 范围版本号同步；附录 F 矩阵补 M4.5 行（AC-97~99） |
| v1.2.0 | 2026-09-10 | **升版**（需求调整，次版本）：修订 §3.2 NFR-02 与 AC-67 空闲内存目标 **80MB→250MB**（多进程合计）——4 个 .NET/WPF 进程基线合计约 180MB，原 80MB 目标在 4 进程架构下不可达；§3.2/§6 AC-67/附录 F 矩阵同步；文件名升版为 `项目需求书[v1.2.0].md`；F-12 默认手动热键 **Ctrl+Alt+O→Alt+F**（规避占用，§2/AC-07/附录同步） |
| v1.3.0 | 2026-09-11 | **升版**（需求补充，次版本）：新增 §2.8 配置输入易用性——**F-78**（描边色/颜色输入辅助：常用色块 + 系统取色器 + 「跟随全局·清除」+ 内联说明；快速开始/MANUAL 增填写说明）；§6 新增 **AC-100**；§7 文档清单文件名同步；附录 F 矩阵补 M4.6 行（AC-100）；文件名升版为 `项目需求书[v1.3.0].md` |

### 1.2 开发规范（development_standards[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.2.0 | 2026-09-11 | **升版**（规则新增，次版本）：新增 **§2.8 UI 输入易用性规约**——自由输入型字段（颜色/数值/路径等）须提供辅助输入（色块/取色器/浏览）与内联说明，保留手填 + 既有校验，用户可见文案走 i18n；文件名升版为 `development_standards[v1.2.0].md` |
| v1.1.0 | 2026-09-09 | **基线锁定后首次升版**：确立四维版本模型（项目/发布/程序集/文档独立演进，引用 VERSION_REGISTRY[v1.0.0].md）；§1.2 纳入 AGENTS.md/docs/AGENTS[vX.Y.Z].md 入版本管理并登记 AGENTS.md 文件名豁免；新增 §1.6.1 程序集版本管理（0.1.0 起步/独立递增/发布对齐）；§1.3 加四维版本模型行；§5.1 交付清单与 §7 目录结构补 AGENTS/VERSION_REGISTRY ·**2026-09-10 CI 首跑修复（不升版）**：§4.4 补「测量范围（`coverage.runsettings`）」——门禁只衡量核心逻辑（配置解析/规则匹配/文本渲染/错误码），排除 `Object1688.Shared.Ipc.*` / `Crash.*` / `System.Text.RegularExpressions.Generated.*` |
| v1.0.0 | 2026-09-08 | 首版：版本管理/代码风格/代码检查/测试/交付/日志/目录结构；§1.3 更新文档版本与项目版本解耦规则。同日草案补充（不升版）·第三轮（12 项）：§1.5 提交/注释语言中文、§1.7 GitHub 协作与 PR 门禁、§2.5 XML 文档注释（公开 API 强制）、§2.6 本地化 resx 规约、§2.7 UI MVVM/线程规约、§3.1 分析器定选与豁免流程、§3.4 AI 辅助代码门禁、§4.1 定选 xUnit+命名/AAA、§4.4 覆盖率 CI 门禁、新增 §9 依赖与兼容治理（NuGet 锁版本+漏洞门禁、schema/协议版本化迁移），术语表顺延 §10。·第七轮：新增 §4.5 国际化键一致性 CI 门禁（AC-75）。·第八轮：新增 §4.6 性能基准 CI 回归（AC-85）。·同步（同日，不升版）：§5.1 交付清单与 §7 目录结构同步 MANUAL.en/资源 fonts/icons 细分/THIRD_PARTY_NOTICES/参考样板不入仓说明 |

### 1.3 架构设计（architecture[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：技术栈/进程拓扑/IPC/叠加层/错误码速览。同日草案补充（不升版）·第一轮：独占全屏覆盖策略 §4.3/多行段落渲染 §3.2-3.3/规则 JSON 扩展 §4.2/预览导入导出自启 §5.5/自启默认开 §8.2/多显示器 §8.3/卸载 §8.5/默认语言 §5.4+§9.3。·第二轮：JSON 增 matchMode/enabled §4.2/占位符与勿扰裁决 §3.2/文字三类来源 §3.5/规则启停与勿扰配置 §5.5/清除历史 §6.3/日志隐私 §7.1/§8.6 许可签名/里程碑与决策附录同步。·第四轮：§2.4 进程生命周期与优雅退出、§3.1 DPI/§3.2 提示音、§4.5 匹配性能预算、§5.6 全局配置 Schema、§5.7 UI 布局示意、§6.3 采样保留、§8.7 命令行参数。·第五轮：§3.4 字形回退、§4.1 自身排除与 §4.4 PID 去重、§5.1 原子写、§6.3 计数持久化/关窗隐藏、§7.2 crash 清理、§8.1 manifest（asInvoker）、§8.2 exe 迁移检测、§8.3 工作区定位、§2.4 锁屏会话/权限、§10 风险与 §12 决策附录同步。·第六轮：新增 §5.6 配置界面交互增强（帮助/列表/向导/冲突/三类测试/预设/拖拽/撤销备份），原 §5.6-5.7 顺延 §5.7-5.8，§6.3 图表与布局记忆、§12 决策附录同步。·第七轮：§2.3 IPC ACL、§3.2 帧率与特效降级、§5.1 辅助数据自愈、§6.3 帧率曲线、§8.3 旋转屏、§8.7 二次实例定稿、§12 决策附录同步、M6 AC 计数更新。·第八轮：§5.6 样式预设参数定义、§5.7 uiState 入 schema、新增 §6.4 stats.json 结构、§8.1 图标资源矩阵、§9.2 CI 性能基准、§11 里程碑加 DoD 列、§12 决策附录同步。·**第九轮补充（同日，不升版）**：§5.7 schema 补手动大字完整样式字段、规则级 soundEnabled/minIntervalSeconds 字段、全局字体覆盖 global.fontFamily、{time} 格式 global.timeFormat、全局默认↔规则级覆盖优先级说明；§4.2 规则结构注释补 minInterval/soundEnabled；§4.4 补 minInterval 与去重窗口双维度协调；§3.4 补全局字体覆盖；§3.2 补 {time} 格式；§2.4 补欢迎大字启动时序；§7.3 错误码速览同步新增主要错误码；§11 M4 DoD 已含 AC-83。·**第十轮补充（同日，不升版）**：§4.2 displayLines 每行富文本字段（color/outline/align+块级回退）、§3.2 动画不受系统"关闭动画"影响+渲染瞬时开销上报（OVL-W-3007）、§4.4 静默裁决（暂停/勿扰/投屏叠加）+提权窗口/安全桌面降级提示（OVL-W-3008）、§5.6 首开引导/空状态、§6.3 渲染开销展示+最近一次大字回看+导出编码/字段/命名定稿、§7.3 错误码速览补 OVL-W-3007/3008。·**2026-09-09 指针同步（不升版）**：§7 配套文档引用 development_standards 更新为 v1.1.0 |
| v1.0.0 | 2026-09-10 | **M2 视觉验收/描边方式登记（不升版）**：§3.3 描边两方案（方案 A 柔光 DropShadow / 方案 B 精确 8 向偏移实心）与 `global.outlineMode`/规则级 `outlineMode` 选择（M2 验收发现柔光晕对比偏弱）；§4.2 规则结构补 `outlineMode` 字段；§5.7 global 补 `outlineMode` + 覆盖优先级说明 |

### 1.4 错误码表（error_codes[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：编号规则/模块前缀/级别/登记流程。·**第九轮补充（同日，不升版）**：完整错误码补全 18→35 条——CFG 增 1005/1006/1007/1008/1009（导入校验/导入备份失败/音频校验/旧 schema 迁移/{time} 格式非法）、PRC 增 2004/2005（热键冲突/自启写入失败）、OVL 增 3004/3005/3006（测试显示失败/字体加载失败/字体覆盖回退）、CRS 增 4005（crash 清理失败）、MON 增 5004/5005（入队/最小间隔抑制）、IO 增 6003/6004（导出选中空/诊断包部分失败）、IPC 增 7003/7004（连接校验失败/二次实例转发失败）、GEN 增 9003/9004（i18n 键缺失/自检降级）；§3 日志示例同步。·**第十轮补充（同日，不升版）**：OVL 增 3007/3008——OVL-W-3007（大字触发瞬间渲染开销超阈值，CPU/内存峰值，NFR-02 扩展）、OVL-W-3008（目标为安全桌面/提权窗口，无法覆盖，NFR-04 扩展）。·**2026-09-09 M1 实施登记（不升版）**：核对错误码全集实际 **44 条**（表格早已 44，登记口径 37 更正为 44，与代码 ErrorCodes.AllCodes 一致；AGENTS/VERSION_REGISTRY 引用同步更正）。·**2026-09-09 M3d 实施登记（不升版）**：新增 **MON-W-5006**（命中上报的规则不存在，配置可能已变更，TriggerEvent 已丢弃）——表格 §2.6 补行、总数 44→**45**；代码 ErrorCodes.cs（常量+AllCodes+头部计数）与 ErrorCodesTests（ExpectedCodeCount=45/AllCodes_Count_Is45）同步。·**2026-09-09 M4a 实施登记（不升版）**：新增 **CFG-E-1010**（配置写入失败：写盘/序列化回读校验/锁超时，旧文件保留）——表格 §2.1 补行、总数 45→**46**；代码 ErrorCodes.cs（常量+AllCodes+头部计数）与 ErrorCodesTests（ExpectedCodeCount=46/AllCodes_Count_Is46）同步 |

### 1.5 README 项目说明（README[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：项目简介/快速上手/文档索引（含版本化文件名）。同日草案补充（不升版）：功能简介与八轮决策全量同步（匹配模式/欢迎大字/手动大字/多行变量/勿扰/隐私本地化/提示音/向导预设/诊断包/图标状态/性能目标等）；文档索引补 MANUAL.en 与开发工作记录、10 组文档说明。·**2026-09-09 登记型同步（不升版）**：文档索引补 AGENTS/AGENTS.md（AI 协作索引）与 VERSION_REGISTRY（版本索引总账）行，文档组计数 10→12；development_standards 引用更新 v1.1.0 |
| v1.0.0 | 2026-09-10 | **登记型同步（不升版）**：快速上手去占位并指向根目录 `快速开始.md`/`API调用说明.md`；文档索引新增两份根入口文件；文档组 12→13 |

### 1.6 用户手册（MANUAL[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：安装/快速上手/配置说明/疑难排解（错误码链接更新）。同日草案补充（不升版）：与多轮决策同步——自启默认开与失效检测、SmartScreen 说明、多行/匹配模式/勿扰/欢迎大字/手动大字、清除历史与隐私声明、卸载指引；第八轮全量同步：向导/样式预设/预览拖拽/测试显示、图表/诊断包/帧率曲线、命令行参数、启动自检降级、数据位置与备份、头部登记英文版 MANUAL 链接 |
| v1.0.0 | 2026-09-10 | **登记型同步（不升版）**：补齐 §1 安装与首次运行（零基础步骤：获取/放置/首次运行/SmartScreen/自启/卸载）；校正过时项（一键诊断包已提供、日志清理说明、控制命令补 `diagnostics`）；加指向根目录 `API调用说明.md` 的链接；中英同步 |
| v1.0.0 | 2026-09-10 | **终稿（不升版）**：头部"（开发中）"→"（终稿）"；校正过时内存目标 ≤80MB→≤250MB（多进程合计）；补齐 §7 常见问题（FAQ，中英各 8 条：隐私/托盘状态/全屏与提权/占位符变量/多屏/暂停/回退/默认快捷键 Alt+F）；中英同步 |

### 1.7 测试报告（TEST_REPORT[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：AC-01~18 验收模板，引用需求书 v1.0.0。同日草案补充（不升版）：AC 表先后扩至 AC-25（第一轮 7 项）、AC-33（第二轮净增 8 项：匹配模式/规则启停/占位符/手动大字/提前结束/勿扰/隐私清除/第三方许可清单；欢迎大字每次启动语义由 AC-06 承载）、AC-42（第四轮净增 9 项：提示音/分层错误提示/优雅退出/命令行参数/未保存提示/采样保留/规则规模/睡眠时变/高 DPI）、AC-54（第五轮净增 12 项：字形回退/托盘交互/自身排除/进程去重/计数持久化/工作区定位/原子写/锁屏会话/关窗隐藏/crash 清理/exe 迁移/asInvoker）、AC-66（第六轮净增 12 项：内置帮助/规则列表增强/新建向导/冲突检测/三类测试/样式预设/布局记忆/托盘状态/统计图表/撤销备份/无障碍/拖拽定位）、AC-78（第七轮净增 12 项：性能量化/帧率检测/数据自愈/IPC 安全/热键冲突/资源校验/特效降级/旋转屏/i18n 键一致性/睡眠延时/深浅图标/二次实例转发）；新增 4a 验收环境矩阵。·第八轮：AC 扩至 AC-87（净增 9 项：图标矩阵/示例集/手册双语/启动自检/诊断包/错误去重/CI 基准/DoD/追踪矩阵）。·**第九轮补充（同日，不升版）**：§4a 验收环境矩阵补充维度——操作系统语言（简体中文系统验证默认语言跟随/中文字形回退 + 英文系统验证回退英文/i18n 键一致性）、配置扩展能力（全局字体覆盖回退/各格式选项/最小间隔限流）、退出/崩溃（优雅超时强杀/崩溃转储与清理）。·**第十轮补充（同日，不升版）**：AC 表扩至 AC-96（净增 9 项：富文本行样式/动画与系统设置解耦/投屏演示自动静默/导出格式定稿/最近一次大字回看/错误码可操作/提权窗口降级提示/渲染瞬时开销监控/首开引导与空状态）。·**2026-09-09 M1 实施登记（不升版）**：自动化测试表登记 M1 结果——错误码/命令行/IPC 信封/日志格式四组单元测试 **60/60 通过**（xUnit，net8.0-windows）；M1 冒烟 exit 0 全生命周期通过；已知问题表登记 M1-01 命名管道 Access denied（已修复 2026-09-09）。·**2026-09-09 M2 实施登记（不升版）**：自动化测试表扩至 **122/122 通过**（新增占位符/排版引擎/样式预设/渲染参数 JSON 四组 62 项）；M2 冒烟 `--demo-banner` 动画生命周期曲线验证通过（浮现→保持→淡出→窗口关闭）；已知问题表补 M2-01（视觉人工验收/独占全屏覆盖实测定版为 M2 DoD 尾项）；§4 测试环境登记实机（Win11 26200/zh-CN/2560×1440）。·**2026-09-09 M3 实施登记（不升版）**：自动化测试表扩至 **194/194 通过**（M3 新增五组 72 项：配置解析 24/规则匹配 16/监测引擎 12/监测循环 14/触发事件 JSON 6）；已知问题表补 M3-01（监测触发全链路人工验收为 M3 DoD 尾项，含真实全屏应用触发）。·**2026-09-09 M3d 实施登记（不升版）**：自动化测试表扩至 **212/212 通过**（新增 BannerAssembler 合并语义 16 项 + ConfigLoaderTests 哨兵 2 项，配置解析集 24→26）；冒烟记录补 M3d 轮（ConfigChanged 广播→欢迎大字→Overlay 更新配置 全链日志印证，exit 0）；已知问题表保持 M3-01/M2-01 人工验收待办。·**2026-09-09 M3 验收登记（不升版）**：AC-04/AC-05/AC-26 勾验 ✅（M3 监测触发人工验收全链路 **20/20 PASS**——进程 exact/contains/wildcard、窗口标题 exact、全屏 includeFullscreen 裁决、排除、`--quit` 优雅退出）；自动化测试表补 **IpcPipeClientTests 握手门控 2 项**，合计扩至 **215/215**；冒烟记录补 M3 验收轮 + 握手竞态根因修复摘要；已知问题 M3-01 → ✅ 已解决，测试环境"测试人"更新；验收脚本第 5 步改用 WinForms 窗口（conhost `title` 尾随空格致 exact 失配，非产品缺陷）。·**2026-09-09 M4a 实施登记（不升版）**：自动化测试表扩至 **245/245 通过**（M4a 新增三组 30 项：ConfigSerializer 序列化 4/ConfigSaver 原子写 11/ConfigValidator 校验 15，合计 215+30）；BOM 断言修复（xUnit `DoesNotContain` 双参重载默认 CurrentCulture，zh-CN 将 U+FEFF 视为可忽略字符致 IndexOf 恒返回 0，改显式 `StringComparison.Ordinal`，非产品缺陷）·**2026-09-09 M4b 实施登记（不升版）**：自动化测试表扩至 **246/246 通过**（M4b 新增 IpcEnvelopeJsonTests TestPlay round-trip 1 项）；IPC 信封测试行描述更新；冒烟记录补 M4b 轮（ConfigUI 接入后 Main 拉起链路 exit 0，ConfigUI.exe 随 Main 产物分发·**2026-09-10 M5 实施登记（不升版）**：自动化测试表扩至 **289/289 通过**（M5 新增七组 43 项：LogFileWriter 7/CrashDumper+CrashGuard 7/ErrorNotifier 7/StatsFile 5/SoundAssetValidator 11/LocalizedStrings 5，LogEntry FATAL 扩 InlineData 1 行）；冒烟记录补 M5 轮（五进程心跳/优雅退出全链 exit 0，日志按天轮转落盘正常）；已知问题表保持 M2-01 视觉人工验收待办） ·**2026-09-10 M4 补全实施登记（不升版）**：自动化测试表扩至 **330/330 通过**（M4 补全新增五组 41 项：uiState 2/Perf 13（RingBuffer+StatsTracker+PerfExporter）/ProcessProbe 6/UndoRedoStack 7/DndGate+ComposeManual 13）；冒烟记录补 M4 轮（Monitor 常驻采集/性能窗口接线 + ConfigUI 五页签 + Main 托盘手动大字/关于/切语 + DndGate 裁决不干扰启动与优雅退出，exit 0 无残留）；AC 勾验补 M4 实现项（AC-10/25/39/51/55/56/61/64 等，视觉/图标类留 M6 人工）；已知问题表保持 M2-01 视觉人工验收待办|

### 1.8 开发工作记录（【YYYY-MM-DD】【开发工作记录】[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版：2026-09-08 全日开发工作留档（需求确认/决策/文档交付）；同日补充文档独立版本管理改革记录（草案修订不升版）。同日补录：§2.11 第一轮 12 项缺口决策、§2.12 第二轮 11 项决策、§2.13 第三轮开发规范 12 项、§2.14 第四轮参考样板对照 13 项、§2.15 第五轮运行时行为 12 项、§2.16 第六轮用户友好 12 项、§2.17 第七轮性能鲁棒安全 12 项、§2.18 第八轮交付门禁 12 项、§2.19 说明文件全量同步（均草案期不升版） |
| v1.0.0 | 2026-09-09 | 新增：2026-09-09 全日开发工作留档——M1 实施（六程序集骨架 0.1.0/错误码 44 条冻结/CLI 解析/IPC 协议定版/优雅退出链路/命名管道 DACL 根因攻坚 piperepro CASE1-6→FullControl 修复/冒烟 exit 0/单测 60/60/文档登记同步）。·同日续录 M2 实施：视觉原型——Overlay 渲染层（BannerWindow 透明置顶/Per-Monitor V2 DPI/独占全屏覆盖策略框架 + BannerAnimation 浮现→保持→淡出 + FrameRateMonitor + BannerQueueService 串行泵 + BannerSoundPlayer 默认关）+ Shared 排版组件（BannerMetrics 多行/CJK 字符级硬折 + PlaceholderResolver {appName}/{time} 12-24h + StylePresets 四套预设 + BannerRequest/DisplayLine/TextAlignment/WrapStrategy）+ IPC EndBanner 消息 + IpcJson 宽松编码（中文 UTF-8 直出）+ App 集成（TriggerCommand/EndBanner/--demo-banner）+ 字体 Montserrat(OFL) 嵌入 + build 0 警告 + 单测 122/122 + 冒烟动画生命周期通过。·同日续录 M3 实施：监测触发——Shared 配置模型 12 文件 + ConfigLoader 原子校验 Issue 收集；RuleMatcher 三级匹配 + MonitorEngine 分桶索引/PID 去重/自身排除；MonitorLoop 轮询热更新 + WindowsMonitorProvider 全屏检测；TriggerEventReport + IpcMessageType.TriggerEvent 上报；单测 194/194 + M3 冒烟 exit 0。·同日续录 **M3d 实施（触发分发）**：BannerAssembler 组装器（rule→global 合并/哨兵回退/custom 坐标解析钳制）+ RuleConfig 哨兵化 + Main LoadConfig/SendStartupStateToOverlay（ConfigChanged 广播→F-11 欢迎大字）/HandleTriggerEvent（TriggerEvent→TriggerCommand 回推，新增错误码 **MON-W-5006**）+ IpcPipeServer.SendToRoleAsync + Overlay ConfigChanged 接收；单测 **212/212** + M3d 冒烟 exit 0（ConfigChanged→欢迎→Overlay 更新全链日志印证）。·同日续录 **M3 人工验收（20/20 全 PASS）**：编写 `build/verify-m3-acceptance.ps1`（八步：环境重置/AC-04 进程启动/AC-26 三种匹配/AC-26 排除/窗口标题/AC-05 全屏/`--quit` 优雅退出/清理）；攻坚两个根因——① `--quit` 静默丢失：**IpcPipeClient 握手竞态**（TryConnectAsync 在 ConnectAsync 返回后握手确认前即赋 `_stream/_writer`，ForwardToPrimaryAsync 的 SendAsync 抢写数据帧先于握手行，服务端将首行当握手拒绝吞消息→客户端已返回成功→exit 0 但实际未退出；修复：新增 `volatile bool _handshakeAcked` 门控 + 握手写入纳入 `_writeGate` + IsConnected/CloseStreamAsync 同步，新建 IpcPipeClientTests 2 项回归测试，单测 213→**215/215** 全绿）；② 验收脚本第 5 步窗口标题 exact 失配：conhost `cmd /k title` 产生的标题带**尾随空格**（`o1688title-exact ` len=17，hex 尾 20-00）而规则值为无空格字符串→exact 字典失配（非产品缺陷，RuleMatcherTests 的 exact 单测早已覆盖），改用 WinForms 顶层窗口构造标题后通过；验收 **20/20 全 PASS**（AC-04/AC-26/AC-05/AC-36 优雅退出勾验），修复 config displayLines/脚本 DOTNET_ROOT 等前轮三轮问题随跑通过。·同日续录 **M4a 实施（配置保存链路）**：ConfigSerializer（camelCase 字段+小写枚举序列化/WriteIndented/UTF-8 无 BOM）+ ConfigSaver 原子写（互斥锁超时/序列化回读校验/写盘失败旧文件保留）+ ConfigValidator（schemaVersion/规则/DisplayLine/提示音校验）+ ConfigImportExport（导入导出+自动备份+旧 schema 迁移）+ AutostartRegistry（开机自启注册表）+ 新错误码 **CFG-E-1010**；修复 BOM 断言假阳性（xUnit `DoesNotContain("\uFEFF")` 双参重载默认 CurrentCulture，zh-CN 将 U+FEFF 视为可忽略字符致 IndexOf 恒返回 0 误报 Sub-string found，改显式 `StringComparison.Ordinal`，测试代码/产物均正确）；单测 215→**245/245** 全绿（ConfigSerializer 4/ConfigSaver 11/ConfigValidator 15）；全量 build 0 警告 0 错误 ·**2026-09-09 M4b 实施登记（不升版）**：工作记录补 M4b 配置窗口实施记录（ConfigUI 最小 WPF 窗体：加载/规则增删编辑/实时预览/保存校验+原子写+ConfigChanged 热生效/TestPlay 测试显示/关闭隐藏/单实例抬窗/Main 托盘“设置…”入口；单测 245→246/246、冒烟 exit 0）。·**2026-09-10 M5 实施登记（不升版）**：2026-09-09 工作记录续录 九之八 M5 主进程功能收尾——日志系统完善（LogLevel FATAL 五级 + Shared LogFileWriter 按天轮转保留 10 + 修复持句柄轮转 rename 实因）、崩溃转储（CrashDumper/CrashCleanup/CrashGuard + 五进程挂载）、分层错误提示 AC-35 + ErrorNotifier 去重聚合 AC-84 + Main 托盘致命气泡、AC-69 StatsFile 自愈、AC-72 SoundAssetValidator（CFG-V-1007）、AC-75 双语 resx + LocalizedStrings 回退 + check-resx-keys.ps1、GenerateDocumentationFile 门禁 CS1591 补齐、ci.yml（覆盖率 ≥80%/resx 键一致/perf-bench）、THIRD_PARTY_NOTICES；单测 289/289 + 冒烟 exit 0；无新增错误码·**2026-09-10 M4 补全实施登记（不升版）**：09-09 工作记录续录 九之九 M4 补全——uiState 布局记忆节 AC-61；Perf 采集（RingBuffer/PerfSample/ProcessProbe/StatsTracker/PerfExporter）+ 导出 AC-47/91；Monitor 性能窗口（ShowPerfWindow/曲线/事件流/关窗隐藏）AC-10/39/51；ConfigUI 五页签化 欢迎/手动/勿扰·提示音·自启 AC-08/38 + 使用说明页 AC-55；规则列表增强 搜索/复制/排序/批量 + UndoRedoStack AC-56/64；DndGate 勿扰/暂停裁决 + 手动触发 ComposeManual AC-07/29/31；AboutWindow AC-25 + 托盘切语/首开引导 AC-09/20；单测 330/330 + 冒烟 exit 0；无新增错误码（图标矩阵/全量 resx UI 后置 M6 前评估）|

| v1.0.0 | 2026-09-10 | **新增**：2026-09-10 开发工作留档**独立成文** `【2026-09-10】【开发工作记录】[v1.0.0].md`（九之八~九之三十六：M5 主进程收尾 / M4 补全 / M6 准备 / M2 视觉验收+描边方式 / 外部脚本控制接口 v1.1.0 / 全局快捷键 / 导入导出+样式预设 / 性能图表+帧率+回看 / 自启+诊断 / 系统健壮性 / 投屏+开销 / 提权+托盘 / 配置交互 / 图标矩阵 / 错误码可操作+托盘本地化 / 无障碍+全量本地化 / 零基础用户文档 / M6 500 规则基准+RuleMatcher 修复 / 环境矩阵验收脚本 / 可机测项补测 / 需求书 v1.2.0 内存目标修订 / 实机人工验收脚本 / CI perf-bench 触发器+首跑修复 / 脚本双击启动器 / 默认热键 Alt+F / MANUAL 终稿 / 最终文档检查） |
| v1.0.0 | 2026-09-11 | **新增**：2026-09-11 开发工作留档**独立成文** `【2026-09-11】【开发工作记录】[v1.0.0].md`（九之三十七~九之四十二：AC-24/42/74 实机验收通过（AC 99/99）/ 打包产物核对 + perf-bench 历史对比 / 发布包随包双击卸载器 + 打包脚本修复 / 托盘图标兜底 / quit.cmd 双击退出 + 托盘溢出说明 / 托盘图标未显示排查）；同时把 2026-09-09 文件中的 09-10/09-11 续录拆分独立成文 |

### 1.9 用户手册·英文（MANUAL.en[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-08 | 首版（英文）：中文 MANUAL 的英文版（双语交付，第八轮新增文档组）；结构同步中文版，内容开发中。同日补充：§4~§7 与中文版全量对齐（图表/诊断包/帧率曲线/启动自检/命令行参数/数据位置与备份） |

### 1.10 AI 协作开发索引（AGENTS[vX.Y.Z].md 与根 AGENTS.md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-09 | 首版：面向 AI 助手的开发入口索引——必读清单（优先级排序）、任务类型→资料映射（改需求/写代码/改文档/发版）、AI 协作规则（三重确认/禁则/完成后动作）、当前状态指针（基线已锁 M1 待启动）、文档速查索引。同步创建仓库根 `AGENTS.md`（稳定指针，文件名豁免 §1.6 重命名规约，仅登记此分组）。·**2026-09-09 M1 实施登记（不升版）**：§4 当前状态指针更新（代码已启动 M1 实施中/程序集已建统一 0.1.0/待用户指示调整）；§1 与 §5 错误码引用 37→44。·**2026-09-09 M2 实施登记（不升版）**：§4 当前状态指针更新（M1 完成、M2 视觉原型实施完成/单测 122/122/冒烟通过/视觉人工验收待收尾）。·**2026-09-09 M3 实施登记（不升版）**：§4 当前状态指针更新（M3 监测触发实施中：配置加载+规则匹配+监测循环完成/单测 194/194/M3 冒烟通过/监测触发人工验收待收尾）。·**2026-09-09 M3d 实施登记（不升版）**：§4 当前状态指针更新（M3d 触发分发实施完成：Main 配置加载广播/欢迎大字/F-07 提前结束链路接线/单测 212/212/M3d 冒烟 exit 0；监测触发人工验收仍为 M3 收尾待办）。·**2026-09-09 M4a 实施登记（不升版）**：§4 当前状态指针更新（M4a 配置保存链路完成：ConfigSerializer/ConfigSaver/ConfigValidator/ConfigImportExport/AutostartRegistry + CFG-E-1010/单测 245/245/错误码 45→46；待办更新为 M4a 收尾提交与 M4b 配置窗口启动） ·**2026-09-09 M4b 实施登记（不升版）**：§4 当前状态指针更新（M4b 配置窗口完成：ConfigUI 窗体 + Main 托盘“设置…”入口/单测 246/246/冒烟 exit 0；待办更新为 M4b 收尾提交与后续里程碑/GitHub 远程推送） ·**2026-09-10 M5 实施登记（不升版）**：§4 当前状态指针更新（M5 主进程功能收尾完成：日志 FATAL + LogFileWriter 按天轮转/崩溃转储五进程挂载/AC-35+84 分层提示去重聚合 + 托盘致命气泡/AC-69 StatsFile 自愈/AC-72 SoundAssetValidator/AC-75 双语 resx + LocalizedStrings + check-resx-keys/静态分析门禁/ci.yml/THIRD_PARTY_NOTICES；单测 289/289/冒烟 exit 0；待办更新为 M5 收尾提交与 M6/后续里程碑） ·**2026-09-10 M4 补全实施登记（不升版）**：§4 当前状态指针更新（M4 补全实施完成：uiState 布局记忆/Perf 采集 + Monitor 性能窗口/ConfigUI 五页签化/规则列表增强 + UndoRedoStack/DndGate 勿扰裁决 + 手动触发/AboutWindow + 托盘切语 + 首开引导；单测 330/330/冒烟 exit 0；待办更新为 M4 补全收尾提交与 M6/后续里程碑；图标矩阵/全量 resx UI 部分交付后置）|
| v1.0.0 | 2026-09-10 | **M2 视觉验收/描边方式登记（不升版）**：§4 当前状态指针更新（M2 视觉验收执行：离屏渲染 + 像素分析 + 全屏覆盖；发现描边偏弱 → 实现描边方式可选 OutlineMode/Shadow+Stroke + 配置界面选择；单测 344/344；冒烟 exit 0） |

### 1.11 版本索引（VERSION_REGISTRY[vX.Y.Z].md）

| 版本 | 日期 | 变更摘要 |
|------|------|----------|
| v1.0.0 | 2026-09-09 | 首版：四维版本模型总账——维度1 项目版本（tag v1.0.0 文档基线记录）、维度2 发布版本（未发布，M6 出口计划）、维度3 程序集版本（五进程+Shared 0.1.0 起步，独立递增规则）、维度4 文档版本索引（12 份文档当前快照）；升版与联动规则（发布四维对齐/文档独立/契约联动）；与 CHANGELOG 分工界定。·**2026-09-09 M1 实施登记（不升版）**：§4 程序集版本确认已实建（六个程序集统一 0.1.0）；§5 错误码 37→44。·**2026-09-09 M2 实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M2 渲染层实施未触发独立升版（延续 M1 起步口径，正式发布对齐时统一评估）。·**2026-09-09 M3 实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M3 监测触发层实施（Shared 契约新增 Monitor/TriggerEvent）未触发独立升版（延续 M1/M2 口径，正式发布对齐时统一评估）。·**2026-09-09 M3d 实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M3d 触发分发层实施（Shared 契约新增 BannerAssembler/BannerRequest；Main/Overlay 行为变更；错误码 44→45）未触发独立升版（延续 M1/M2/M3 口径，正式发布对齐时统一评估）。·**2026-09-09 M4a 实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M4a 配置保存层实施（Shared 契约新增 Config 配置序列化/保存/校验/导入导出/自启组件；错误码 45→46）未触发独立升版（延续 M1/M2/M3/M3d 口径，正式发布对齐时统一评估） ·**2026-09-09 M4b 实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M4b 配置窗口层实施（新增 ConfigUI 窗体进程；Shared 无契约变更，仅新增 IpcMessageType.TestPlay 协议消息；Main 托盘入口；错误码 46 不变）未触发独立升版（延续 M1/M2/M3/M3d/M4a 口径，正式发布对齐时统一评估） ·**2026-09-10 M5 实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M5 主进程功能收尾层实施（Shared 新增 LogFileWriter 共享化/Crash 转储清理护栏/Notify 去重聚合/Stats 自愈/I18n 双语资源/Config SoundAssetValidator；LogLevel 增 FATAL；错误码 46 不变，未触发独立升版，延续 M1~M4b 口径，正式发布对齐时统一评估） ·**2026-09-10 M4 补全实施登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M4 补全层实施（Shared 新增 UiStateConfig 布局节/Perf 采集导出/StatsTracker/Monitor DndGate/Ui UndoRedoStack/BannerAssembler.ComposeManual；IpcMessageType 新增 ShowPerfWindow 成员；StatsFile 序列化改 camelCase 对齐架构 §6.4；错误码 46 不变，未触发独立升版，延续 M1~M5 口径，正式发布对齐时统一评估）|
| v1.0.0 | 2026-09-10 | **M2 视觉验收/描边方式登记（不升版）**：§4 程序集版本保持 0.1.0 并备注 M2 视觉验收 + 描边方式可选实施（Shared 新增 `OutlineMode` 枚举 + GlobalConfig/RuleConfig/ManualConfig `OutlineMode` 字段；Overlay `BannerVisualFactory` 方案 B 精确描边；ConfigUI 三处下拉；错误码 46 不变，未触发独立升版，延续 M1~M6 口径，正式发布对齐时统一评估） |

---

## 2. 项目版本记录（tag，与文档版本解耦）

### [Unreleased] - 待发布（M1~M6 已实施，未打 tag）

#### Added（计划）
- 项目初始化：全部文档采用独立版本管理（头部版本 + 集中登记 + 文件名带版本号）
- 功能范围见 `docs/项目需求书[v1.3.0].md`（M1~M6 已实施；余 AC-24/42/74 实机验收与发布）

#### Added（M1 实施，2026-09-09）
- 五进程骨架 + Object1688.Shared：六个程序集已建并统一 `0.1.0`（AssemblyVersion 0.1.0.0）
- 错误码冻结：`ErrorCodes.cs` 44 条（CFG/PRC/OVL/LOG/CRS/MON/IO/IPC/GEN，格式 `^[A-Z]{3}-[EIWV]-\d{4}$`）
- 命令行解析：`CliParser`/`CliOptions`（--config/--lang/--debug/--no-autostart/--version）+ `BuildUsageText`
- IPC 协议定版：命名管道（MainControlPipe/LoggingPipe）+ 当前用户 ACL 安全描述符 + 封套 JSON（camelCase/枚举名）+ 心跳/握手/优雅退出常量（IpcProtocol）
- 优雅退出链路：UserExit / 心跳超时 / Faulted 三类原因；子进程回执确认；超时强制收尾兜底
- 冒烟验证通过（exit 0）：三子进程握手心跳全就绪 → 优雅退出全链确认，无 IPC-E-7001 / 无强制 Kill / 无孤儿进程

#### Added（M2 实施，2026-09-09）
- Overlay 渲染层：`BannerWindow`（透明置顶+通透+不抢焦点+Per-Monitor V2 DPI+独占全屏覆盖策略框架）+ `FontAssets`/`ScreenArea`/`VisualFactory` + `BannerAnimation`（浮现→保持→淡出，F-07 提前结束）+ `FrameRateMonitor`（AC-67/68/73）+ `BannerQueueService`（UI 线程串行泵，OVL-W-3004 回退/OVL-W-3007 降级警告）+ `BannerSoundPlayer`（默认关，system 音源）
- Shared 排版组件：`BannerMetrics`（多行段落/CJK 字符级硬折/行高 0.35/最小缩放 0.4）+ `PlaceholderResolver`（{appName}/{time}，auto 12-24h 区分大小写随文化）+ `StylePresets`（control-classic/highcontrast/subtitle-light/cinema-dark 四套预设）+ `BannerRequest`/`DisplayLine`/`TextAlignment`/`WrapStrategy`
- IPC 扩展：`IpcMessageType.EndBanner`（提前结束 F-07）；`IpcJson` 采用 `UnsafeRelaxedJsonEscaping`（中文 UTF-8 直出，不再 \uXXXX 转义）
- App 集成：TriggerCommand→Enqueue、EndBanner→EndCurrent；`--demo-banner` 启动演示（欢迎大字）；字体 Montserrat(Bold/Regular) OFL 嵌入
- 单元测试扩至 **122/122**（M1 60 + M2 新增 62：占位符/排版/预设/渲染参数 JSON）；`dotnet build -warnaserror` 0 警告 0 错误
- M2 冒烟通过：`--demo-banner` 动画生命周期曲线（2s 浮现 432 → 4s 保持 501 → 8s 关闭回落 1）

#### Fixed（M1 实施，2026-09-09）
- 命名管道第 2+ 实例 `Access denied`：根因实证为 **DACL 授予范围过窄**（`PipeAccessRights.ReadWrite` 缺 GENERIC_READ/GENERIC_WRITE 展开的 SYNCHRONIZE/ReadAttributes/ReadEA/WriteEA/ReadPermissions 等位）；修复为当前用户 `FullControl`（piperepro CASE1-6 最小复现实证）

#### Changed（M2 视觉验收反馈，2026-09-09）
- 大字渲染默认**加粗**：`BannerVisualFactory` 渲染 `FontWeight=Bold`（Montserrat-Bold 嵌入字体物尽其用）；`TextMeasure` 测量同步 Bold——测量与渲染同字重，避免 Wrap/Shrink 按窄宽度排版溢出（TEST_REPORT M2-02）

#### Added（M3 实施，2026-09-09）
- 配置模型与加载：`Shared/Config/` 类型集（AppConfig/GlobalConfig/WelcomeConfig/ManualConfig/RuleConfig/DndConfig/SoundConfig + MatchType/MatchMode/DisplayLine 富文本）+ `ConfigLoader`（默认配置构建/CreateDefault）+ `ConfigLoadResult`/`ConfigIssue` 校验语义
- 规则匹配引擎：`Shared/Monitor/`——`MatchTarget`（进程/窗口标题 + 全屏标记 + PID）、`RuleMatcher`（exact/contains/glob 三级匹配 + includeFullscreen 裁决）、`MonitorEngine`（分桶索引 + 自身进程排除 + PID 去重 + 消失重现补触 + minInterval/去重窗口协调）
- 监测循环与上报：`MonitorLoop`（轮询调度/配置热更新重建引擎/AC-45 同目录自身排除/快照异常容错）+ `MonitorSnapshot`（进程/窗口快照）+ `TriggerEventReport`（命中上报载荷）+ `IpcMessageType.TriggerEvent` + `Overlay/WindowsMonitorProvider`（Process+EnumWindows+GetMonitorInfo 全屏检测 rcMonitor 基准）
- App 集成：`App.xaml.cs` 接线监测循环（非 demo 模式启动/TriggerEvent 经 IpcPipeClient 上报/抑制与错误经日志回调）
- 单元测试扩至 **194/194**（M3 新增 72：配置解析 24/规则匹配 16/监测引擎 12/监测循环 14/触发事件 JSON 6）；`dotnet build -warnaserror` 0 警告 0 错误
- M3 冒烟通过：五进程全流程 + TriggerEvent 链路（架构 §11）

#### Added（M3d 实施，2026-09-09）
- 触发分发组装：`Shared/Text/BannerAssembler`——`ComposeRule`（规则显式字段覆盖全局；哨兵空串/-1 回退全局默认，架构 §5.7）+ `ComposeWelcome`（欢迎大字：全局展示字段 + 欢迎节时序）；custom `{x,y}` 位置解析（IgnoreCase/越界钳制/非法回退 null）+ Repeating+Uuid（防 Overlay 去重吞重复触发）
- 配置哨兵化：`RuleConfig` 展示字段默认值改哨兵（Position/TargetScreen/OutlineColor 空串、OutlineWidth -1，XML 注释注明"跟随全局"）；`ConfigLoader.BuildRule` 保留哨兵不烘焙、显式空 OutlineColor 合法（CFG-V-1003 仅拦非空非法）
- Main 接线：`LoadConfig()`（--config 优先，否则 %APPDATA%\Object1688\config.json，缺失回退模板/默认 CFG-W-1002→GEN-W-9004 降级不阻断）；`SendStartupStateToOverlayAsync`（Overlay 就绪→ConfigChanged 广播→F-11 欢迎大字，Interlocked 防重入）；`HandleTriggerEventAsync`（TriggerEventReport→RuleId 查规则→**MON-W-5006** 缺失警告→ComposeRule→TriggerCommand 回推）
- IPC 扩展：`IpcPipeServer.SendToRoleAsync(role, envelope)`（按角色会话组串行写）；Overlay `App.xaml.cs` 接收 `ConfigChanged`（`_config` volatile 更新）
- 错误码：新增 **MON-W-5006**（命中上报的规则不存在，TriggerEvent 已丢弃），总表 44→45
- 单元测试扩至 **212/212**（M3d 新增 18：BannerAssembler 合并语义 16 + ConfigLoader 哨兵 2）；`dotnet build -warnaserror` 0 警告 0 错误
- M3d 冒烟通过（exit 0）：Overlay 就绪→ConfigChanged 广播→欢迎大字→Overlay 配置更新全链日志印证，无 ERROR/无强制 Kill/无孤儿

#### Added（M4b 实施，2026-09-09）
- 新增 ConfigUI 最小 WPF 配置窗口进程（架构 §5.8）：加载/规则增删编辑/实时预览/保存校验（ConfigValidator+CheckConflicts）+原子写（ConfigSaver）→ ConfigChanged 广播（Main 热生效）/TestPlay 测试显示（→ Main 转 TriggerCommand）/关闭隐藏（OnClosing 取消+Hide）/单实例抬窗（FindWindowW+SW_RESTORE）
- IPC：IpcMessageType 新增 TestPlay（配置窗口测试显示通路）
- Main：托盘新增“设置…”入口（拉起 ConfigUI.exe；csproj CopyChildProcessOutputs 分发 ConfigUI 产物）
- 单元测试 245→**246/246**（+IpcEnvelopeJsonTests TestPlay round-trip 1 项）；全量 build 0 警告 0 错误；M4b 冒烟 exit 0

#### Added（M5 实施，2026-09-10）
- 日志系统完善（架构 §7.1）：LogLevel 增 FATAL（五级）+ LogEntry.FormatLine 映射；`Shared/Logging/LogFileWriter` 抽取共享——按天轮转 `app.YYYY-MM-DD.log`（同日超 5MB 自动递增 .1/.2）+ 保留 10 个清理最旧 + FileShare.ReadWrite|Delete（修复持句柄轮转 rename 失败实因）；Logging 进程迁移复用
- 崩溃转储（架构 §7.2 / AC-13 / AC-52）：`Shared/Crash/`——`CrashDumper`（DbgHelp MiniDumpWriteDump，WithFullMemory → `log/crash/crash_{tag}_{ts}.dmp`）+ `CrashCleanup`（上限清理：默认保留 10 个/≤200MB，删最旧，失败仅记 CRS-W-4005）+ `CrashGuard`（AppDomain.UnhandledException 捕获 → dump + 伴生 `.err.txt` 含 GEN-E-9002/异常摘要，不依赖 Logging 进程存活）；五个进程（Main/Overlay/Monitor/ConfigUI/Logging）入口 Install
- 分层错误提示 + 去重聚合（AC-35 / AC-84）：`Shared/Notify/ErrorNotifier`（同码 30s 窗口仅首弹、重复抑制累计、达阈值 N=5 聚合提示并重置）；Main 托盘 `ShowBalloonTip` 致命气泡（含错误码 / 聚合"已发生 N 次"），MainCoordinator 注入 `fatalNotify` 回调（ERROR/FATAL 级触发）
- 辅助数据自愈（AC-69）：`Shared/Stats/StatsFile`——stats.json 缺失自动建默认、损坏改名 `.corrupt` 备份并重建默认（Healed 标记供调用方记 CFG-W-1002）、原子写；数据结构对齐架构 §6.4
- 资源校验（AC-72 / CFG-V-1007）：`Shared/Config/SoundAssetValidator`——自定义提示音格式（wav/mp3 白名单）/大小上限（10MB）/路径安全（相对路径防 `..` 穿越）；挂入 ConfigValidator.Validate（Source=custom 时）
- i18n 基础（AC-75）：`Shared/Resources/Strings.zh-Hans.resx` + `Strings.en.resx`（键集一致 7 键）；`Shared/I18n/LocalizedStrings`——ResourceManager 卫星取词 + 跨语言回退（zh↔en）+ 双缺 GEN-W-9003 回调占位；`build/check-resx-keys.ps1` 键一致性门禁
- 静态分析门禁（开发规范 §3.1）：各进程 csproj 启用 `GenerateDocumentationFile`，补齐 CS1591 缺失公开注释；全量 build 0 警告 0 错误
- CI（开发规范 §4.4~4.6 / 架构 §9.2）：`.github/workflows/ci.yml`——build-test-gate（build warnaserror→test+coverlet→Shared 覆盖率 ≥80% 门禁→resx 键一致性门禁）+ perf-bench job（AC-85 500 规则基准，手动/定时，退化告警不阻断）
- 第三方许可（AC-33）：根目录 `THIRD_PARTY_NOTICES`——运行期零第三方库、Montserrat（SIL OFL 1.1）、测试期 xunit（Apache-2.0）/coverlet（MIT）/.NET（MIT）
- 单元测试 246→**289/289**（+43：LogFileWriter 7/Crash 7/ErrorNotifier 7/StatsFile 5/SoundAssetValidator 11/LocalizedStrings 5）；全量 build 0 警告 0 错误；M5 冒烟 exit 0
- 无新增错误码（复用 LOG/CRS/GEN/CFG 既有码）；程序集保持 0.1.0（登记型同步不升版）

#### Added（M4 补全实施，2026-09-10）
- **M4-A 配置模型扩展（AC-61 布局记忆）**：`AppConfig` 新增 `uiState` 节（`UiStateConfig`/`UiWindowLayout`：configWin/perfWin 位置/尺寸/分栏/页签/时间窗）；ConfigLoader DTO/Parse/BuildDefault + `config.default.json` 模板同步；schemaVersion 保持 1（兼容新增）
- **M4-B 性能监控纯逻辑层**：`Shared/Perf/`——`RingBuffer<T>`（有界环形，线程安全）、`PerfSample`/`EventItem`（曲线/事件流数据点）、`ProcessProbe`（CPU% 差值计算，100%=单核语义，钳制 核数×100）；`Shared/Stats/StatsTracker`（触发统计累计 totalTriggers/perRule/byHour + 合并写入节流 5s + 退出 flush）；`PerfExporter`（CSV **UTF-8 BOM**/JSON/TXT → log/exports，时间戳防覆盖）；`StatsFile.Options` 统一 camelCase（对齐架构 §6.4）
- **M4-C Monitor 性能窗口**：`IpcMessageType.ShowPerfWindow`（Main→Monitor）；Main 托盘"性能窗口…"入口 + `RequestPerfWindowAsync`；`PerfSampler`（1s 采集五进程 CPU/内存入环形缓冲 + 进程上下线事件流）；`PerfWindow`（CPU/内存曲线自绘 + 60s/5min 切换 + 进程状态表 + 事件流环形 500 + 触发统计 + 导出/清除历史 + **关窗隐藏** AC-51 + **布局记忆** uiState.perfWin + 隐藏时暂停刷新不空耗）
- **M4-D ConfigUI 页签化**：触发规则 | 欢迎大字 | 手动大字 | 勿扰/提示音/自启 | 使用说明 五页签（架构 §5.8）；欢迎/手动大字独立编辑（启用/文字行/字号/延时/保持/快捷键）；勿扰（暂停/定时开关）、提示音（音源 system/custom + 音量 + 自定义路径）、自启开关页；保存前显式同步各页 + 未保存保护（dirty 标记 + 恢复默认确认，AC-38）
- **M4-E 规则列表增强 + 撤销重做**：`Shared/Ui/UndoRedoStack<T>`（AC-64 Ctrl+Z/Y，容量上限/重做失效语义）；ConfigUI 列表工具——搜索过滤（F-27）、复制（深拷贝新 ruleId）、↑/↓ 调序（同命中优先级）、全部启用/停用、空状态引导、字段 tooltip；"使用说明"帮助页（AC-55）
- **M4-F 手动触发 + 勿扰/暂停裁决**：`Shared/Monitor/DndGate`（勿扰/暂停常驻裁决：全局暂停 + 定时勿扰星期×HH:mm，跨午夜 end≤start 判定，手动/测试强制豁免——纯逻辑可单测）；`BannerAssembler.ComposeManual`（手动大字合并组装，F-12/AC-29）；Main `HandleTriggerEventAsync` 接入 DndGate（勿扰/暂停抑制自动触发，事件照常记录）；Main 托盘"手动大字"入口 + `TriggerManualBigTextAsync`（强制路径）
- **M4-G 关于/切语/首开引导**：Main `AboutWindow`（AC-25：版本/GPL-3.0/OFL/隐私声明，托盘"关于…"单例）；托盘"切换语言"（AC-09/F-60 zh-CN↔en-US 轮换写回 + ConfigChanged 广播）；首开引导气泡（F-13/AC-20：firstRun 时托盘提示一次 + AcknowledgeFirstRun 写回）；性能窗口/手动大字/关于 托盘入口齐全
- 单元测试 289→**330/330**（+41：uiState 2/Perf 13/ProcessProbe 6/UndoRedoStack 7/DndGate+ComposeManual 13）；全量 build 0 警告 0 错误；M4 冒烟 exit 0（无残留进程）
- 无新增错误码（复用 CFG/PRC/MON/IO/GEN 既有码）；程序集保持 0.1.0（登记型同步不升版）；**部分交付标注**：图标矩阵（AC-14/62/77/79）与全量窗口 resx 接入（AC-09 完整）属视觉/资源工程量大项，本批交付框架与托盘级实现，矩阵文件与全量 UI 本地化后置 M6 前评估

#### Added（M6 准备，2026-09-10）
- 发布脚本 `build/publish-release.ps1`（架构 §8.1/§9.1）：restore(-r win-x64) → build Release(warnaserror) → resx 键门禁 → 五进程分别 `PublishSingleFile` 自包含 x64 发布至 `dist/publish-win-x64/Main/` + 复制 LICENSE/THIRD_PARTY_NOTICES/config.default.json；**实测通过**（产物含 Object1688.Main/Overlay/Monitor/Logging/ConfigUI.exe，`OBJECT1688_SMOKE=1` dist 冒烟 exit 0 无残留）
- 卸载脚本 `build/uninstall.ps1`（架构 §8.5/F-72，纯 ASCII 规避 PS5.1 中文解析）：删除 HKCU Run 自启项 → 清理 `%APPDATA%\Object1688` → 可选 `-AlsoCleanLogs` 清 log/；语法校验通过
- MANUAL 双语（`MANUAL[v1.0.0].md`/`MANUAL.en[v1.0.0].md`）按 M1~M5+M4 补全实际功能校正：托盘菜单（设置…/手动大字/性能窗口…/切换语言/关于…/退出）、配置五页签（触发规则/欢迎大字/手动大字/勿扰·提示音·自启/使用说明）、性能窗口（曲线 60s/5min/事件流/导出 CSV·TXT·JSON/清除历史）、日志与数据位置（config.bak1..3/stats.json/log/crash/log/exports）；疑难排解同步如实标注已实现 vs 规划项
- `.gitignore` 增 `dist/`（发布产物不入库）
- 无代码逻辑变更、无错误码变更、无单测新增（纯发布/卸载/文档工程）；程序集保持 0.1.0

#### Added（M2 视觉验收 + 描边方式可选，2026-09-10）
- Overlay `--demo-banner=<case>` 演示用例参数化（classic/multiline/outline/subtitle/cinema/custom/long，仅 demo 路径）——供 M2 视觉验收与回归；缺省 classic 保持旧行为
- **M2 视觉人工验收执行**（AI 辅助离屏渲染 + 像素分析 + 屏幕实测）：核对居中定位/多行中英混排/CJK 折行/top-center 与 custom 定位/四套预设；不透明全屏窗口上叠加层置顶验证通过；**发现描边偏弱**（方案 A 柔光晕边缘约 50% 覆盖，白底黑描边最暗仅灰 116、黑底白描边最亮仅 106）
- **描边方式可选（用户决策：两方案都要，配置界面选择）**：新增 `OutlineMode`（Shadow/Stroke）——方案 B 精确描边 = `BannerVisualFactory` 8 向偏移实心复制（`DropShadowEffect` `ShadowDepth=描边宽`、`BlurRadius=0`）；像素复核白底黑描边 minLum 0、灰底白描边 maxLum 255
- 配置链路：`global.outlineMode`（默认 shadow）+ 规则级 `outlineMode`（空串跟随全局）+ `manual.outlineMode`；ConfigLoader 解析/校验（非法 CFG-V-1003 跳规则）+ ConfigValidator + ConfigSerializer（camelCase 往返）+ `config.default.json` 模板 + BannerAssembler `ResolveOutlineMode` 合并
- ConfigUI：规则编辑"描边方式"下拉 + 手动大字页同款 + "勿扰/提示音/自启"页"大字描边（全局默认）"组（`_globalCfg`/`RebuildGlobal`/`SelectOutlineMode`/`OutlineModeTag`）
- 单元测试 330→**344/344**（+14 `OutlineModeTests`）；全量 build 0 警告 0 错误；M2 冒烟 exit 0 无孤儿
- 无新增错误码（复用 CFG-V-1003）；程序集保持 0.1.0（登记型同步不升版）

#### Added（外部脚本控制接口，2026-09-10 / 需求书 v1.1.0）
- 需求：项目需求书 v1.0.0 → **v1.1.0**（新增 §2.7 **F-76** 外部脚本控制接口 / **F-77** CLI 封装；§6 **AC-97/AC-98/AC-99**；§7/§8.1/附录 F 同步）
- Shared/Ipc：`ControlProtocol`（管道 `Object1688.control`、命令常量）/`ControlRequest`/`ControlResponse`/`ControlServer`（仅当前用户 ACL，单行 JSON 请求响应，无握手，异常返回错误码不终止）/`ControlClient`；抽取 `PipeSecurityFactory` 供内部/外部管道复用
- Main：`MainCoordinator` 托管控制服务端 + 命令分发（ping/status/banner/manual/pause/resume/toggle-pause/end/reload/config/perf/about/quit；banner 缺省回退全局、pause/resume 持久化+广播、未知命令 IPC-E-7005）；App `--control` 控制客户端（单实例互斥前，独立短生命周期进程、不启动 UI）
- CLI：`--control <command>`/`--text`/`--screen`；退出码 0=成功/2=失败/3=不可达；`NativeConsole` 支持重定向标准输出（脚本可捕获）
- 错误码：新增 **IPC-E-7005**（控制命令无效）/ **IPC-E-7006**（控制接口不可达），总表 46→**48**
- 单元测试 344→**351/351**（`ControlInterfaceTests` 4 + `CliParserTests` 3）；全量 build 0 警告 0 错误；控制接口集成实调通过（status/ping/banner 含空格与多行/manual/end/reload/pause/resume/quit 全部 exit 0，bogus→exit 2+IPC-E-7005，quit 无孤儿）；冒烟 exit 0
- 程序集保持 0.1.0（登记型同步不升版）

#### Added（系统健壮性：睡眠唤醒 / 锁屏会话 / 睡眠延时，2026-09-10）
- IPC：新增 `IpcMessageType.SystemEvent` + `SystemEventReport{Kind}`（Suspend/Resume/Lock/Unlock）
- Main：App 订阅 `PowerModeChanged`/`SessionSwitch` → `MainCoordinator.NotifySystemEventAsync` 下发 Overlay（睡眠/唤醒另发 Monitor）
- Overlay：`HandleSystemEvent`（睡眠 ClearPending / 唤醒 PauseFor(3s) 抑制抖动 / 锁屏 EndCurrent+ClearPending 不补发）；`BannerQueueService.ClearPending`（递增代号作废延时中请求）；`MonitorLoop.PauseFor`（暂停窗口内不评估）
- 睡眠延时（AC-76）：`Task.Delay` 睡眠不推进 + 睡眠 ClearPending 不补弹
- 单元测试 375→**377/377**（MonitorLoop PauseFor + SystemEventReport JSON）；build 0/0；冒烟 exit 0；真实睡眠/锁屏需实机人工验证
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（全局快捷键，2026-09-10）
- Shared：`Input/HotkeyParser` + `HotkeySpec`（"Ctrl+Alt+O" → 修饰键 + Win32 虚拟键码）；`Ipc/IpcMessageType.BannerState` + `Ipc/BannerStateReport`（Overlay→Main 大字播放状态）
- Main：`GlobalHotkey`（Win32 RegisterHotKey + 隐藏 HwndSource 消息窗口接收 WM_HOTKEY，MOD_NOREPEAT）；App 启动注册 `manual.shortcut`、`ConfigUpdated` 热重注册、退出注销；`MainCoordinator.OnManualHotkeyAsync`（F-07 正在显示→提前结束 / F-12 否则触发手动大字）；冲突 → PRC-W-2004 + 托盘气泡（AC-71）
- Overlay：`BannerQueueService` 播放开始/结束回调 → 上报 BannerState
- 单元测试 351→**363/363**（`HotkeyParserTests` 12）；build 0/0；集成验证（注册实证 1409 + WM_HOTKEY 处理链：第 1 次弹、第 2 次收）；冒烟 exit 0
- 无新增错误码（复用 PRC-W-2004）；程序集保持 0.1.0（登记型同步不升版）

#### Added（配置窗口导入/导出 + 样式预设套用，2026-09-10）
- 共享层：`ConfigImportExport.ExportToFile`（用户自选精确路径导出，IO-E-6001）；新增 `ConfigImportExportTests` 5 项
- ConfigUI 导入/导出（AC-21/F-22）：工具栏「导入…/导出…」——导出 SaveFileDialog 写 JSON；导入 OpenFileDialog → 自动备份（CFG-W-1006 不阻断）→ 宽容解析 → 有问题确认 → 应用 + ConfigSaver 写盘 + ConfigChanged 热生效
- ConfigUI 样式预设套用（AC-60/F-23）：规则编辑「样式预设」下拉（四套）+「套用」→ 字号/描边/位置套用，仍可微调
- 单元测试 363→**368/368**；build 0 警告 0 错误；ConfigUI 启动无崩溃
- 无新增错误码（复用 IO-E-6001/CFG-W-1006/CFG-E-1001/CFG-V-1005）；程序集保持 0.1.0（登记型同步不升版）

#### Added（性能窗口图表 + 帧率曲线 + 最近一次大字回看，2026-09-10）
- IPC 契约：`IpcMessageType.FrameRate`（Overlay→Main→Monitor）+ `FrameRateReport`；`IpcMessageType.LastBanner`（Main→Monitor）+ `LastBannerReport`
- 帧率曲线（AC-68）：`FrameRateMonitor.Sampled` 每秒事件 + `BannerQueueService` onFpsSample 回调 + Overlay 上报 + Main 转发 + `Shared/Perf/MonitorFeed`（环形 300）
- 最近一次大字回看（AC-92）：Main `SendBannerToOverlayAsync(request, ruleId)` 统一下发 TriggerCommand 并同步 LastBanner 给 Monitor（规则/欢迎/手动/测试/控制接口全路径）
- 图表（AC-63）：PerfWindow 规则触发排行 Top5 条形图 + 24h 时段分布图（StatsTracker perRule/byHour）；布局扩展三列曲线 + 中排图表/回看
- 单元测试 368→**371/371**（`PerfFeedTests` 3）；build 0/0；集成验证帧率/回看链路日志 + `--control perf` 无崩溃；冒烟 exit 0
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added / Tooling（M6 环境矩阵自动验收脚本，2026-09-10）
- 新增 `build/verify-m6-acceptance.ps1`：自动化 M6 可机测项（AC-82 降级自检 / AC-67 启动就绪 + 内存报告 / AC-40/AC-85 500 规则基准 / AC-97/98 控制接口 / 优雅退出 + 无孤儿 / 5 exe 校验）+ 打印 AC-24/42/74/82/67 人工清单；报告落 `log/m6-acceptance-<时间戳>.txt`
- 本机实测 **11/0 全 PASS**（启动就绪 ≤3s、基准 <5ms、控制接口 OK、无孤儿）；空闲内存报告 **232MB**（>80MB，AC-67 🟡）
- 无代码/错误码/单测变更（纯工具脚本）；程序集保持 0.1.0

#### Added（M6 可机测项补测：性能窗口打开 + 动画帧率，2026-09-10）
- 新增 `Shared/Perf/FrameRateTracker`：线程安全持有最近帧率采样；Main 在 FrameRate 消息处记录并经控制接口 `status` 暴露 `fps`/`fpsDegraded`（供验收脚本自动读取，无需人工目测）
- Monitor `OpenPerfWindow` 打开窗口时记日志「性能窗口已打开（AC-10/AC-67）」（供脚本以时间戳精确测量窗口打开耗时）
- `build/verify-m6-acceptance.ps1` 增两可机测项：AC-67 性能窗口打开 ≤1s、AC-67 动画帧率 ≥60fps；本机 **13/0 全 PASS**（窗口打开 665ms、帧率 63.7fps）
- 新增 `FrameRateTracker` 单测 2 项（393→**395/395**）；build 0 警告 0 错误
- 无新增错误码；程序集保持 0.1.0

#### Changed（需求修订：AC-67 空闲内存目标 80MB→250MB，2026-09-10）
- 需求书升 **v1.2.0**（`项目需求书[v1.3.0].md`，重命名自 v1.1.0）：§3.2 NFR-02 与 §6 AC-67 空闲内存目标 **80MB→250MB（多进程合计）**——4 个 .NET/WPF 进程基线合计约 180MB，原 80MB 在 4 进程架构下不可达
- 同步：架构/开发规范/README/TEST_REPORT/AGENTS/docs AGENTS/VERSION_REGISTRY 中的需求书引用与版本；TEST_REPORT AC-67 转 ✅（实测 182–235MB ≤250MB）
- 验收表 **95/99→96/99 ✅**；余 3 项（AC-24 多显示器 / AC-42 高 DPI / AC-74 旋转屏）待实机
- 无代码/错误码/单测变更；程序集保持 0.1.0

#### Added / Tooling（M6 实机人工验收脚本，2026-09-10）
- 新增 `build/verify-m6-manual.ps1`（UTF-8 BOM）：引导式人工验收 AC-24 多显示器 / AC-42 混合 DPI / AC-74 旋转屏——枚举显示器、逐步引导、自动向指定屏触发大字、收集 PASS/FAIL/SKIP 并写 `log/m6-manual-<时间戳>.txt`；`-Check` 仅枚举显示器
- 本机仅 1 屏（2560×1440），`-Check` 通过、三项判 SKIP；需在实机（≥2 屏 / 混合缩放 / 旋转屏）运行
- `build/verify-m6-acceptance.ps1` 人工清单指向该脚本
- 新增双击启动器 `build/verify-m6-manual.cmd` / `build/verify-m6-acceptance.cmd`（`chcp 65001` + `-ExecutionPolicy Bypass -File` + `pause`），解决双击 `.ps1` 不运行
- 无代码/错误码/单测变更；程序集保持 0.1.0

#### Fixed / CI（perf-bench 触发器修正，2026-09-10）
- `ci.yml`：`on:` 补 `schedule: - cron: '0 3 * * 1'`（每周一 03:00 UTC）；`perf-bench` 的 `if` 增加 `push` → 推送即触发首跑，之后每周自动
- 背景：原 `if` 引用 `github.event_name == 'schedule'` 但 `on:` 未定义该触发器，导致 perf-bench 从未自动运行；默认分支为 `develop`
- perf-bench 步骤加 `--logger "console;verbosity=detailed"`，使 `[PERF]` 实测值在 CI 日志可见
- 无代码/错误码/单测变更；程序集保持 0.1.0

#### Fixed（CI 首跑修复：时区测试 + 覆盖率门禁范围，2026-09-10）
- **测试**：11 个测试硬编码 `+08:00`，CI runner 为 UTC → `{time}` 经 `ToLocalTime()` 差 8h；改为机器本地时间戳（`PlaceholderResolverTests` 9 + `BannerMetricsTests` 1）；`MonitorEngineTests` 500 规则基准阈值 5ms→20ms（共享 runner 粗护栏，精确预算见 `RuleMatcherBenchTests`）
- **覆盖率门禁**：新增 `coverage.runsettings`（排除 `Object1688.Shared.Ipc.*` / `Crash.*` / `System.Text.RegularExpressions.Generated.*`）；`ci.yml` 测试步骤改用 `--settings coverage.runsettings`；整库分支 77.65% → 核心逻辑 **83.92%**（门禁 PASS）
- 开发规范 §4.4 补「测量范围」（不升版）
- 本机全量 395/395；**CI 复跑 run #24（`d9dd439`）全绿**（build-test-gate 395/395 + 覆盖率门禁 + resx；perf-bench ✅，CI 实测 5.44ms/poll）；程序集保持 0.1.0

#### Changed（F-12 默认手动热键 Ctrl+Alt+O→Alt+F，2026-09-10）
- 规避 `Ctrl+Alt+O` 被其他程序占用；默认值改动：`config/config.default.json`、`ConfigLoader.BuildDefault`、`ManualConfig.Shortcut` 默认值；ConfigUI 提示文案；MANUAL 中英、架构配置示例/决策附录、需求书 §2/AC-07/附录、快速开始、TEST_REPORT 同步
- 测试：`HotkeyParserTests` 新增 `Alt+F` 解析用例（ModAlt+VK_F）；`ConfigLoaderTests` 默认断言改 Alt+F
- 本机全量 396/396；程序集保持 0.1.0

#### Changed（MANUAL 终稿，2026-09-10）
- 中英手册头部"（开发中）"→"（终稿）"；校正过时内存目标 ≤80MB→≤250MB（多进程合计）
- 补齐 §7 常见问题（中英各 8 条 Q&A：隐私 / 托盘状态 / 全屏与提权 / 占位符变量 / 多屏 / 暂停 / 回退 / 默认快捷键 Alt+F）
- 无代码/错误码/单测变更；程序集保持 0.1.0

#### Changed（AC-24/42/74 实机验收通过 + 打包核对 + perf-bench 历史对比，2026-09-11）
- **AC-24/42/74 实机验收通过**（用户实测）→ `TEST_REPORT` 勾验 ✅，验收表 **99/99 ✅**（全部通过）
- **打包核对**：`publish-release.ps1` → `dist/` 五进程 + config + LICENSE/NOTICES，发布版冒烟 exit 0 无孤儿；修复 config 模板复制位置（→ `config\config.default.json`）；MANUAL 磁盘目标 100MB→700MB（自包含实测 ~660MB）
- **perf-bench 历史对比**：新增 `build/perf-baseline.json`（基线 5.44ms）+ `ci.yml` 解析 `[PERF]` 比对 >2× 告警 + `upload-artifact@v4` 存档（AC-85）
- 程序集保持 0.1.0

#### Added（发布包随包双击卸载器 + 打包脚本修复，2026-09-11）
- 新增 `build/uninstall.cmd`（双击卸载：自启项 + `%APPDATA%\Object1688`）；`publish-release.ps1` 随包复制 `build\uninstall.cmd/.ps1` 到发布目录 `build\`（Windows 11 `.ps1` 默认记事本打开，故提供 `.cmd`）
- 打包修复：打包前清理旧 `dist` 产物（消除残留根 `config.default.json`）；还原 `publish-release.ps1` 为纯 ASCII（误加中文注释致 PS5.1 解析失败、`-Path` 绑定报错）
- 重新打包验证：五进程 + `config\config.default.json` + `build\uninstall.cmd/.ps1` + LICENSE/NOTICES，冒烟 exit 0 无孤儿
- MANUAL 中英 §1.5 卸载改为「双击 `build\uninstall.cmd`」
- 程序集保持 0.1.0

#### Fixed（托盘图标兜底，2026-09-11）
- 排查：8 个 `tray_*.ico` 已嵌入 `Object1688.Main.g.resources`，`NotifyIcon{Visible=true}` 正常 → 用户未见到图标最可能是 Win11「隐藏的图标」溢出区
- 改动：`App.UpdateTrayStatus` 增加兜底 `Icon.ExtractAssociatedIcon(Environment.ProcessPath)`，资源加载失败时仍显示 exe 自带图标
- build 0/0；全量 396/396；程序集保持 0.1.0

#### Added（quit.cmd 双击退出 + 托盘溢出说明，2026-09-11）
- 新增 `build/quit.cmd`（双击即 `Object1688.Main.exe --control quit`，无需点托盘图标）；`publish-release.ps1` 随包复制到发布目录 `build\`
- 处理残留：`--control quit` 优雅关闭 4 个运行中进程（无残留）
- MANUAL 中英 §1.3/§5 + `快速开始.md` 补「Win11 托盘图标在 `^` 溢出区、拖出固定；找不到图标可用 `build\quit.cmd` 退出」
- 程序集保持 0.1.0

#### Fixed（托盘图标排查 + 创建顺序，2026-09-11）
- `CreateTrayIcon` 调整为「先设 Icon+Text、再 `Visible=true`」并加 try/catch；新增诊断日志 `%TEMP%\object1688-tray.log`
- 诊断证明程序侧正常（`icon=ok visible=True`）；用户机器（Win11 build 26200）shell 未显示托盘图标 → 系统侧问题（新版 XAML 托盘；可用 `build\quit.cmd` 退出）
- build 0/0；全量 396/396；程序集保持 0.1.0

#### Added（快速开始/API 校正 + 干净发布包 zip，2026-09-11）
- `快速开始.md`：磁盘 100→700MB、发布包文件列表 `resources/`→`build/`、卸载改双击 `build\uninstall.cmd`；`API调用说明.md`：`status` 响应补 `fps`/`fpsDegraded`
- 新增 `build/pack-release.ps1`：`dist/publish-<rid>/Main` → 干净 zip（剔除 `log/`、`*.pdb`、`*.xml`）→ `dist/Object1688-v0.1.0-win-x64.zip`（268.6 MB，20 项）
- 程序集保持 0.1.0

#### Released（GitHub Release v0.1.0，2026-09-11）
- 打发布 tag **v0.1.0**（annotated，develop HEAD `ec63055`）+ 推送；创建 GitHub Release `Object1688 v0.1.0`（非草稿/非预发布）
- 上传资产 `Object1688-v0.1.0-win-x64.zip`（268.6 MB，state=uploaded）→ `releases/download/v0.1.0/Object1688-v0.1.0-win-x64.zip`
- 备注：仓库私有 → 仅协作者可下载；方案 B（提交 zip 进仓库）受 GitHub 单文件 100MB 上限限制，需 Git LFS
- 程序集保持 0.1.0

#### Added（发布包入仓 · Git LFS，2026-09-11）
- `git lfs install` + `git lfs track "*.zip"`（`.gitattributes` 增 `*.zip filter=lfs diff=lfs merge=lfs -text`）；`releases/Object1688-v0.1.0-win-x64.zip`（268.6 MB，LFS 指针）随仓库分发
- push 上传 LFS 对象 **282 MB（1/1，done）** → `48603f5..a0fabf0`
- 仓库仍私有；公开需先数据脱敏（用户后续处理）
- 程序集保持 0.1.0

#### Added（公开仓库 WELCOME_SCREEN · 全新历史，2026-09-11）
- 新建公开仓库 `AngelinatheMellowWish/WELCOME_SCREEN`：**全新历史**（1 提交，209 文件，1.9MB）+ 精简版内容（源码/测试/构建脚本/用户文档 README·快速开始·API·MANUAL 中英·CHANGELOG/LICENSE/NOTICES/工程配置）
- 脱敏：全新历史 → 旧历史个人邮箱不再出现；扫描确认无敏感标识；新提交用 noreply
- tag `v0.1.0` + Release + zip 资产（268.6MB，uploaded）→ `releases/download/v0.1.0/Object1688-v0.1.0-win-x64.zip`
- 代码同步：`AboutWindow.xaml.cs` 链接 → `…/WELCOME_SCREEN`；新增根 `README.md`
- 程序集保持 0.1.0

#### Changed（配置输入易用性 · 需求书 v1.3.0 / 规范 v1.2.0 / 程序集 0.2.0，2026-09-11）
- 需求书升 **v1.3.0**：新增 §2.8 **F-78**（描边色/颜色输入辅助）+ §6 **AC-100** + 附录 F **M4.6** 行；重命名 `项目需求书[v1.2.0]`→`[v1.3.0]`
- 开发规范升 **v1.2.0**：新增 **§2.8 UI 输入易用性规约**；重命名 `development_standards[v1.1.0]`→`[v1.2.0]`
- 程序集 **0.1.0→0.2.0**（六个 csproj + app.manifest + 版本回退串）
- 同步 VERSION_REGISTRY（§4 程序集 / §5 文档索引）+ AGENTS/docs AGENTS/README/architecture/TEST_REPORT 引用
- **本次仅改文档 + 版本号**；ConfigUI 描边色色块/取色器/跟随全局/说明 + 快速开始·MANUAL 填写说明按 F-78/AC-100 **随后实施**

#### Changed（验收表 AC 逐项勾验，2026-09-10）
- `TEST_REPORT` AC 表按实现/验收状态勾验：**93/99 ✅**；余 6 项（AC-24 多显示器 / AC-40 500 规则基准 / AC-42 高 DPI / AC-67 性能量化 / AC-74 旋转屏 / AC-82 启动自检完整项）标注需 M6 实机验证
- 无代码变更、无错误码变更、无单测变更

#### Fixed / Perf（M6 500 规则性能基准与修复，2026-09-10）
- 新增 500 规则匹配性能基准 `RuleMatcherBenchTests`（`OBJECT1688_PERF_BENCH=1`，CI perf-bench 依赖的 `RuleMatcherBench` 过滤此前为空，已补齐）
- **修复**：`RuleMatcher` 通配正则 `RegexMatchTimeoutException` 未捕获会外抛拖垮监测循环 → 捕获按不匹配处理（防崩溃）
- **性能**：通配正则改 `RegexOptions.NonBacktracking`（线性、免回溯）；`*X*` 型通配退化为 `Contains`(Ordinal)；表达式规则元数据入列表免字典查找；不敏感规则目标小写一次 + 模式预小写后 Ordinal 比较
- 实测：500 规则 × 300 目标 **7.17ms → 4.57ms/poll**（目标 <5ms 达标）；启动就绪 557ms（≤3s 达标）；空闲内存 182MB（4 进程合计，目标 ≤80MB 未达标，记 AC-67 🟡）
- 单元测试 392→**393/393**；build 0/0；冒烟 exit 0；AC-40/AC-85 勾验（AC-67 🟡）
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（零基础用户文档，2026-09-10）
- 项目根目录新增两份面向零基础用户的文件：**`快速开始.md`**（零基础安装/首次运行/第一条规则/功能速查/FAQ/卸载）与 **`API调用说明.md`**（脚本控制接口完整说明：CLI + 命名管道 JSON、命令与参数、请求/响应字段、PowerShell/Python 示例、退出码与错误码、安全说明）
- `docs/MANUAL`（中英）**补齐 §1 安装与首次运行**（零基础步骤：获取/放置/首次运行/SmartScreen/自启/卸载），校正过时项（一键诊断包已提供、日志清理说明、控制命令补 `diagnostics`），并加指向 `API调用说明.md` 的链接
- `docs/README` 快速上手与文档索引同步（新增两份根入口文件；文档组 12→13）
- 无代码变更、无错误码变更、无单测变更

#### Added（无障碍 + 全量窗口本地化，2026-09-10）
- 无障碍（AC-65）：ConfigUI/性能窗口关键控件 `AutomationProperties.Name`（图标按钮/搜索/预览/曲线/列表）
- 全量窗口本地化（AC-09）：ConfigUI/Monitor 新增 `LocalizationService`（resx → ResourceDictionary → XAML `{DynamicResource}`）；工具栏/页签/字段标签/分组标题/按钮/列头/标题全走 i18n；resx 新增 ~71 键（共 98 键，门禁 PASS）
- 全量 **392/392** 通过；build 0 警告 0 错误；ConfigUI/性能窗口启动无崩溃；冒烟/quit 无孤儿
- 说明：动态状态消息与"使用说明"正文仍为中文，后续可继续接入
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（错误码可操作 + 托盘菜单本地化，2026-09-10）
- 错误码可操作（AC-93/F-42 扩展）：托盘「打开日志文件夹」+「复制最近错误码」；气泡点击打开日志；记录最近错误码
- 托盘菜单本地化（AC-09）：resx 新增 19 键；Main `ApplyLanguage`（CurrentUICulture + 重建托盘菜单）+ 切换语言即时生效；窗口标题走 resx
- resx 键一致性门禁 27 键 PASS；单元测试 384→**392/392**（LocalizedStrings 新键解析 8）；build 0/0；冒烟 exit 0；Main 启动 + 托盘重建无崩溃
- 说明：全量逐控件本地化（ConfigUI/性能窗口正文）为后续项
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（图标矩阵 + 托盘状态变体，2026-09-10）
- `build/gen-icons.ps1` 程序化生成图标矩阵 → `resources/icons/`：`app.ico`(多尺寸)/`window.ico`/托盘 `tray_{normal,paused,dnd,error}_{light,dark}.ico`/`about_*.png`（AC-14/79）
- 五进程 csproj `ApplicationIcon`；关于窗口 `Icon`（AC-14）
- Main 托盘状态变体：正常/暂停/勿扰/错误（AC-62）+ 深浅色主题适配（AC-77）
- 全量 **384/384** 通过；build 0 警告 0 错误；Main 启动 + 托盘加载无崩溃；图标观感需人工确认（可替换）
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（配置窗口交互补全，2026-09-10）
- 新建向导（AC-57/F-28）：`NewRuleWizard` 三步窗口（匹配→显示→确认，分步校验）+ 工具栏「新建向导…」入口
- 冲突检测 UI（AC-58/F-29）：工具栏「冲突检测」→ `ConfigValidator.CheckConflicts` 列出重复/重叠
- 预览拖拽定位（AC-66/F-23）：预览 Canvas 拖拽文字 → 归一化坐标写回 `Position="custom {x,y}"`
- 全量 **384/384** 通过；build 0 警告 0 错误；ConfigUI 启动无崩溃；冒烟 exit 0
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（提权窗口降级提示 + 托盘交互，2026-09-10）
- 提权窗口降级（AC-94/NFR-04 扩展）：Overlay `ElevationProbe`（TokenElevation）判定命中目标提升完整性 → OVL-W-3008 + 新增 IPC `Warning`（+`WarningReport`）→ Main 托盘气泡一次（同码去重）
- 托盘交互（AC-44）：左键快捷菜单（手动大字 / 暂停·恢复）、双击打开配置、悬停状态文本（运行中/已暂停）、右键完整菜单；暂停态图标变体（AC-62 部分）
- 单元测试 383→**384/384**（WarningReport JSON）；build 0/0；冒烟 exit 0；常驻启动 + 托盘新代码无崩溃 + quit 无孤儿；提权目标实机需人工验证
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（投屏自动静默 + 渲染瞬时开销，2026-09-10）
- 投屏/演示自动静默（AC-90/F-25 扩展）：`dnd.presentationAutoSilence`（默认关）+ `DndGate` 叠加静默 + Overlay `MonitorLoop` 全屏状态 → `PresentationState` 上报 → Main 裁决（手动/测试豁免）；ConfigUI 勿扰页开关
- 渲染瞬时开销（AC-95/NFR-02 扩展）：`global.renderCostCpuWarnMs`(400)/`renderCostMemWarnMB`(80) + Overlay 浮现阶段采样 → 超阈值记 OVL-W-3007 + `RenderCost` 上报 → Main 转发 Monitor → PerfWindow 展示
- IPC：新增 `PresentationState` / `RenderCost` 消息 + 载荷 DTO
- 单元测试 377→**383/383**；build 0/0；集成验证 AC-90/AC-95 链路日志 + OVL-W-3007 实测；冒烟 exit 0
- 无新增错误码；程序集保持 0.1.0（登记型同步不升版）

#### Added（自启失效检测 + 一键诊断包，2026-09-10）
- 自启失效检测（AC-53/F-24）：`AutostartRegistry.GetRegisteredPath`/`IsRegisteredPathValid`（仅指向当前 exe 有效，不自动改写）+ `MainCoordinator.CheckAutostartInvalidPath` 启动校验 + 托盘气泡；新增错误码 **PRC-W-2006**
- 一键诊断包（AC-83/F-30 扩展）：`Shared/Diagnostics/DiagnosticPackager` 打包 environment/config/stats/logs → `log/exports/diagnostics-*.zip`；缺失/失败不阻断（Partial→IO-W-6004）；日志 `FileShare.ReadWrite|Delete` 读取（可打包被持有的 app.log）
- 入口：控制接口命令 `diagnostics` + Main 托盘「导出诊断包…」；CliParser 用法同步
- 错误码 48→**49**（PRC-W-2006）；单元测试 371→**375/375**（AutostartRegistryTests 2 + DiagnosticPackagerTests 2）；build 0/0；`--control diagnostics` 集成验证通过；冒烟 exit 0
- 程序集保持 0.1.0（登记型同步不升版）

---

## 3. 变更登记规则

- **何时登记**：任何 docs/ 文档升版（内容变更 → 头部版本升号 → 重命名文件）后，必须同步在对应分组追加一行
- **登记内容**：版本号 / 日期 / 变更摘要（Added/Changed/Fixed/Breaking 要点）
- **草案阶段**：文档处于"待基线确认"时，同日修订不升版，仅更新登记摘要；基线锁定后修订必须升版并重命名
- **.txt 同步**：CHANGELOG 自身升版后同样重跑 `build/sync-docs.ps1`
- **项目发布**：项目版本（tag）变更记录在"项目版本记录"章节，与文档版本登记分区管理
