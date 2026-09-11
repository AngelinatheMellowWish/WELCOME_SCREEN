# WELCOME SCREEN

> 《Control》风格桌面大字标题叠加程序 —— Windows 后台小工具。

当**指定程序启动**、**程序自身启动**或**手动触发**时，在屏幕所有窗口之上浮现一张仿《Control》风格的白色大字标题，停留数秒后淡出；不打断操作，纯本地运行、不联网。

## 下载
- 最新发布：**https://github.com/AngelinatheMellowWish/WELCOME_SCREEN/releases**
- 解压后**双击 `Object1688.Main.exe`** 即可（常驻系统托盘）。零基础步骤见 [`快速开始.md`](快速开始.md)。

## 主要功能
- 触发规则：进程名 / 窗口标题（完全 / 包含 / 通配），多显示器目标屏
- 欢迎大字 / 手动大字（全局热键，默认 `Alt+F`）/ 可选提示音
- 勿扰 · 定时静默 · 投屏演示自动静默；开机自启（含路径失效检测）
- 配置窗口：新建向导 / 样式预设 / 拖拽预览 / 导入导出 / 冲突检测
- 性能窗口：CPU·内存·帧率曲线 / 事件流 / 触发排行 / 最近一次大字回看
- 外部脚本控制接口：命名管道 JSON + `Object1688.Main.exe --control <命令>`（见 [`API调用说明.md`](API调用说明.md)）
- 多进程隔离架构；崩溃自动转储；完整错误码 + 分级日志

## 文档
- 快速开始（零基础）：[`快速开始.md`](快速开始.md)
- 脚本 / API 调用：[`API调用说明.md`](API调用说明.md)
- 用户手册（中文）：[`docs/MANUAL[v1.0.0].md`](docs/MANUAL[v1.0.0].md)
- User Manual (English): [`docs/MANUAL.en[v1.0.0].md`](docs/MANUAL.en[v1.0.0].md)
- 变更日志：[`docs/CHANGELOG[v1.0.0].md`](docs/CHANGELOG[v1.0.0].md)

## 从源码构建
- 需要 .NET 8 SDK（Windows）。
- 构建：`dotnet build src/Object1688.sln -c Release`
- 打包：`build\publish-release.ps1`（发布）→ `build\pack-release.ps1`（生成 zip）

## 系统要求
- Windows 10 / Windows 11（64 位）

## 许可
- GPL-3.0，见 [`LICENSE`](LICENSE)。
- 第三方依赖与嵌入字体许可见 [`THIRD_PARTY_NOTICES`](THIRD_PARTY_NOTICES)。
