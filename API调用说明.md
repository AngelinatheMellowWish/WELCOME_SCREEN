# API 调用说明 — 用脚本控制 Object1688

> 面向零基础用户：程序**运行期间**，你可以用 PowerShell / Python / 任何能读写文件或启动进程的语言，调用它的功能（弹大字、暂停、结束、查状态、重载配置、退出等）。
> 接口**仅限当前登录用户**访问，纯本地、不联网。

---

## 30 秒示例

在程序正在运行时，打开 PowerShell 执行：

```powershell
# 查状态
& "C:\Object1688\Object1688.Main.exe" --control status

# 弹一张大字
& "C:\Object1688\Object1688.Main.exe" --control banner --text "Hello 世界"
```

> 把 `C:\Object1688\Object1688.Main.exe` 换成你的实际路径。结果以 **JSON** 打印。

---

## 前提

- 程序（`Object1688.Main.exe`）**正在运行**。未运行时调用会失败（退出码 `3`）。
- 与程序**同一登录用户**下执行（跨用户/远程无法访问，属安全设计）。

---

## 方式一：命令行（推荐，最简单）

程序本身提供了一个"控制客户端"入口：启动它并带上 `--control`，它会自动连到正在运行的主程序、执行命令、打印 JSON、然后退出。

### 用法

```
Object1688.Main.exe --control <命令> [--text <文字>] [--screen <primary|索引>]
```

| 参数 | 说明 |
|------|------|
| `--control <命令>` | 要执行的命令（见下表） |
| `--text <文字>` | 仅用于 `banner`：要显示的文字（支持 `\n` 换行） |
| `--screen <primary\|索引>` | 仅用于 `banner`：目标显示器，`primary` 为主屏，或 `1`/`2`… |

### 命令一览

| 命令 | 作用 |
|------|------|
| `ping` | 连通性探测 |
| `status` | 查询运行状态 |
| `banner` | 立即显示任意大字（需 `--text`） |
| `manual` | 触发已配置的"手动大字" |
| `pause` | 全局暂停（不弹自动大字） |
| `resume` | 恢复 |
| `toggle-pause` | 暂停/恢复切换 |
| `end` | 提前结束当前大字 |
| `reload` | 从磁盘重载配置并生效 |
| `config` | 打开配置窗口 |
| `perf` | 打开性能窗口 |
| `about` | 打开关于窗口 |
| `diagnostics` | 导出诊断包 |
| `quit` | 优雅退出程序 |

### 示例

```powershell
$exe = "C:\Object1688\Object1688.Main.exe"

& $exe --control status
& $exe --control banner --text "开会中`n请勿打扰"
& $exe --control manual
& $exe --control pause
& $exe --control resume
& $exe --control end
& $exe --control reload
& $exe --control diagnostics
& $exe --control quit
```

### 输出与退出码

- **输出**：单行 JSON，打印到标准输出。例：
  ```json
  {"ok":true,"data":{"pong":true}}
  ```
- **退出码**：

| 退出码 | 含义 |
|--------|------|
| `0` | 成功（`ok:true`） |
| `2` | 命令执行失败（`ok:false`，响应含 `errorCode`） |
| `3` | 控制接口不可达（主程序未运行） |

> 命令行方式只支持 `--text` / `--screen` 两个参数；如需设置字号、保持时长、描边等，请用**方式二**。

---

## 方式二：命名管道 JSON（更灵活）

直接连接命名管道，发送一行 JSON 请求、读取一行 JSON 响应。适合需要设置更多参数或长时间交互的场景。

- **管道名**：`\\.\pipe\Object1688.control`
- **协议**：单行 JSON 请求 → 单行 JSON 响应（UTF-8）

### 请求字段

| 字段 | 必填 | 说明 |
|------|------|------|
| `command` | ✅ | 命令名（同方式一） |
| `text` | `banner` 时必填 | 显示文字，支持 `\n` 多行 |
| `targetScreen` | 否 | 目标屏：`"primary"`（默认）或 `"1"`/`"2"`… |
| `fontSize` | 否 | 字号（默认取全局 `defaultFontSize`） |
| `holdSeconds` | 否 | 保持秒数（默认 4） |
| `outlineColor` | 否 | 描边色 `#RRGGBB`（默认取全局） |
| `outlineWidth` | 否 | 描边宽度（默认取全局） |
| `outlineMode` | 否 | 描边方式：`shadow`（柔光，默认）/ `stroke`（精确） |

> 未填字段自动回退到程序当前配置的全局默认值。

### 响应字段

| 字段 | 说明 |
|------|------|
| `ok` | 是否成功（`true`/`false`） |
| `data` | 成功时的结果（因命令而异，可空） |
| `errorCode` | 失败时的错误码（如 `IPC-E-7005`） |
| `message` | 失败/补充说明 |

### PowerShell 完整示例

```powershell
function Invoke-Object1688([hashtable]$body) {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'Object1688.control', [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(3000)
    $writer = New-Object System.IO.StreamWriter($pipe); $writer.AutoFlush = $true
    $reader = New-Object System.IO.StreamReader($pipe)
    $writer.WriteLine(($body | ConvertTo-Json -Compress))
    $resp = $reader.ReadLine()
    $pipe.Dispose()
    return $resp | ConvertFrom-Json
}

# 查状态
Invoke-Object1688 @{ command = 'status' }

# 弹一张自定义大字（多行 + 精确描边）
Invoke-Object1688 @{
    command     = 'banner'
    text        = "浏览器`nBrowser"
    fontSize    = 120
    holdSeconds = 5
    outlineColor = '#000000'
    outlineWidth = 3
    outlineMode  = 'stroke'
}

# 暂停 / 恢复
Invoke-Object1688 @{ command = 'pause' }
Invoke-Object1688 @{ command = 'resume' }
```

### Python 完整示例

```python
import json

PIPE = r'\\.\pipe\Object1688.control'

def invoke(body: dict) -> dict:
    with open(PIPE, 'r+b', buffering=0) as f:
        f.write((json.dumps(body, ensure_ascii=False) + '\n').encode('utf-8'))
        line = f.readline()
        return json.loads(line.decode('utf-8'))

print(invoke({'command': 'status'}))
print(invoke({'command': 'banner', 'text': 'Hello 世界', 'holdSeconds': 3}))
print(invoke({'command': 'pause'}))
```

---

## 命令参考（逐个）

### `ping` — 连通性探测
- 参数：无。响应 `data`：`{"pong": true}`

### `status` — 查询运行状态
- 参数：无。响应 `data`：
  ```json
  {
    "running": true,
    "version": "0.1.0",
    "paused": false,
    "language": "zh-CN",
    "configPath": "C:\\Users\\你\\AppData\\Roaming\\Object1688\\config.json",
    "children": { "Logging": "running", "Overlay": "running", "Monitor": "running" },
    "fps": 63.7,
    "fpsDegraded": false
  }
  ```
- `fps`：Overlay 最近一次上报的渲染帧率（尚未上报时为 `-1`）；`fpsDegraded`：是否处于低帧降级态。

### `banner` — 立即显示任意大字
- 参数：`text`（必填，支持 `\n`）、`targetScreen`、`fontSize`、`holdSeconds`、`outlineColor`、`outlineWidth`、`outlineMode`。
- 属**强制路径**，不受暂停/勿扰限制。

### `manual` — 触发已配置的"手动大字"
- 参数：无。内容取配置窗口「手动大字」页的设置。

### `pause` / `resume` / `toggle-pause` — 全局暂停 / 恢复
- 参数：无。`toggle-pause` 响应 `data`：`{"paused": true/false}`。会持久化到配置。

### `end` — 提前结束当前大字
- 参数：无。

### `reload` — 从磁盘重载配置并热生效
- 参数：无。

### `config` / `perf` / `about` — 打开对应窗口
- 参数：无。

### `diagnostics` — 导出诊断包
- 参数：无。响应 `data`：
  ```json
  { "path": "...\\log\\exports\\diagnostics-20260910_120000.zip", "partial": false, "warnings": [] }
  ```

### `quit` — 优雅退出程序
- 参数：无。

---

## 错误码

| 错误码 | 含义 |
|--------|------|
| `IPC-E-7005` | 命令无效（未知命令或参数非法） |
| `IPC-E-7006` | 控制接口不可达（主程序未运行） |
| `GEN-E-9001` | 命令执行时发生未预期异常 |

> 命令失败时响应 `{"ok":false,"errorCode":"...","message":"..."}`，CLI 退出码为 `2`。

---

## 安全说明

- 控制接口通过**命名管道**通信，管道访问控制列表（ACL）**仅授予当前登录用户**（及 SYSTEM/管理员）。
- **不监听网络端口**，其他用户/远程主机无法访问。
- 程序以普通用户权限（asInvoker）运行，接口不会提权。

---

## 常见问题

**Q：调用返回"控制接口不可达"（退出码 3）？**
A：主程序未运行。先启动 `Object1688.Main.exe`。

**Q：提示未知命令？**
A：命令名拼写错误；对照上方"命令一览"。

**Q：`--control banner --text` 中文乱码？**
A：确保终端为 UTF-8；或改用方式二的 JSON（`ensure_ascii=False` / `-Compress`）。

**Q：Python `open` 管道报错？**
A：请用二进制模式 `'r+b'` 且 `buffering=0`，并确保已启动主程序。

---

> 返回零基础上手指南：[`快速开始.md`](快速开始.md) · 完整手册：`docs/MANUAL[v1.2.0].md`
