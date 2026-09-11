#requires -Version 5.1
<#
    M3 验收支撑：AC-04/05/26 真实触发链路自动化验证（架构 §11 / TEST_REPORT M3-01）
    覆盖：
      AC-04  应用启动触发：配置某进程后，启动该进程 → 命中对应规则
      AC-26  匹配模式：exact / contains / wildcard 各按预期命中；无关进程不触发（排除）
      AC-05  全屏触发：真实全屏窗口（borderless 覆盖 rcMonitor）命中 includeFullscreen=true 规则，
            同时被 includeFullscreen=false 规则正确排除，且最大化窗口不算全屏（由生产代码保证，脚本只验触发语义）
      M1/M3d 回归：--quit 二次实例转发 → 优雅退出（无孤儿进程，主进程与次实例均 exit 0）
    用法：  powershell -NoProfile -ExecutionPolicy Bypass -File build/verify-m3-acceptance.ps1
    退出码：0 = 全部通过；1 = 存在失败项
#>
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

<# 关键：本机存在两套 dotnet——C:\Program Files\dotnet（仅 3.1/7.0）与用户级 %LOCALAPPDATA%\Microsoft\dotnet（8.0）。
   若不显式设置 DOTNET_ROOT，apphost（Main 及各子进程）会解析到注册表指向的 Program Files 那套，
   找不到 net8.0 运行时：WinExe（Main）弹"You must install .NET"对话框挂住，控制台子进程（Logging 等）
   秒退 0x80008096 → app.log 永不生成、监测链路全断。此处固定到用户级 8.0 运行时，子进程随进程环境继承。 #>
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

$root      = Split-Path -Parent $PSScriptRoot
$mainBin   = Join-Path $root 'src\Object1688.Main\bin\Debug\net8.0-windows'
$mainExe   = Join-Path $mainBin 'Object1688.Main.exe'
$logFile   = Join-Path $mainBin 'log\app.log'
$legacyLog = Join-Path $mainBin 'log\app.prev.log'
$workDir   = Join-Path $env:TEMP ("o1688-acc-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$testConfig= Join-Path $workDir 'config.acceptance.json'

$pass = 0; $fail = 0
$results = [System.Collections.Generic.List[string]]::new()

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { $script:pass++; Write-Host "  [PASS] $name" -ForegroundColor Green }
    else     { $script:fail++; Write-Host "  [FAIL] $name : $detail" -ForegroundColor Red }
    $results.Add("$name=$ok")
}

<# 日志断言：轮询 app.log 直至匹配（或超时） #>
function Assert-LogMatch([string]$pattern, [int]$timeoutSec = 15, [bool]$mustExist = $true) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    do {
        $content = if (Test-Path -LiteralPath $logFile) { Get-Content -LiteralPath $logFile -Raw -Encoding UTF8 } else { '' }
        if ($mustExist -and $content -match $pattern) { return $true }
        if (-not $mustExist -and $content -match $pattern) { return $false } # 意外出现 → 立即失败
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    return -not $mustExist
}

<# 清场：终止残留 Object1688 进程（仅本产品 exe），归档旧日志 #>
function Reset-Environment {
    Get-Process -Name 'Object1688.Main','Object1688.Overlay','Object1688.Logging','Object1688.Monitor' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    if (Test-Path -LiteralPath $logFile) {
        Move-Item -LiteralPath $logFile -Destination $legacyLog -Force -ErrorAction SilentlyContinue
    }
}

<# 生成验收配置：全部输入规则 + 关闭欢迎/声音干扰 #>
function New-TestConfig {
    $rule = {
        param($id, $type, $value, $mode, $includeFullscreen)
        [ordered]@{
            ruleId              = $id
            matchType           = $type
            matchValue          = $value
            matchMode           = $mode
            enabled             = $true
            matchCaseSensitive  = $false
            includeFullscreen   = $includeFullscreen
            targetScreen        = 'primary'
            displayLines        = @([ordered]@{ text = $id; fontSize = 96 })
            wrapStrategy        = 'shrink'
            delaySeconds        = 0
            holdSeconds         = 3
            position            = 'center'
            outlineColor        = '#000000'
            outlineWidth        = 0
        }
    }

    $rules = @(
        (& $rule 'r-ac04'     'process'     'o1688ac04app.exe'       'exact'     $true)
        (& $rule 'r-exact'    'process'     'o1688exactapp.exe'      'exact'     $true)
        (& $rule 'r-contains' 'process'     'o1688contain'           'contains'  $true)
        (& $rule 'r-wild'     'process'     'o1688wild_*.exe'        'wildcard'  $true)
        (& $rule 'r-title'    'windowTitle' 'o1688title-exact'       'exact'     $true)
        (& $rule 'r-full-allow' 'windowTitle' '*o1688fullscreen*'    'wildcard'  $true)
        (& $rule 'r-full-deny'  'windowTitle' '*o1688fullscreen*'    'wildcard'  $false)
    )

    $config = [ordered]@{
        schemaVersion = 1
        global = [ordered]@{
            defaultFontSize     = 96
            defaultPosition     = 'center'
            defaultOutlineColor = '#000000'
            defaultOutlineWidth = 0
            targetScreen        = 'primary'
            monitorPollIntervalMs = 500
            dedupeWindowSeconds = 2
            logLevel            = 'INFO'
            logMaxSizeMB        = 5
            logRetainCount      = 10
            dpiAwareness        = 'per-monitor-v2'
        }
        # welcome/manual 均为 DTO required displayLines（缺省会抛 JsonException → 整体回退默认配置）
        welcome = [ordered]@{ enabled = $false; displayLines = @([ordered]@{ text = 'welcome-disabled'; fontSize = 96 }) }
        manual  = [ordered]@{ enabled = $false; displayLines = @([ordered]@{ text = 'manual-disabled'; fontSize = 96 }) }
        rules = $rules
        dnd = [ordered]@{ paused = $false; scheduleEnabled = $false; schedule = @() }
        sound = [ordered]@{ enabled = $false; source = 'system'; customPath = ''; volume = 80 }
        autostart = $false
        firstRun  = $false
    }

    New-Item -ItemType Directory -Path $workDir -Force | Out-Null
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $testConfig -Encoding UTF8
    $testConfig
}

<# 复制出真实可执行目标（进程名 = 复制后的文件名，含 .exe）。
   Win11 的 notepad.exe 系启动器存根（重定向商店应用，进程名会变），故用 cmd.exe 副本。
   无参数启动 cmd 副本 → 交互控制台常驻，进程名保持为副本文件名。 #>
function New-TargetExe([string]$name) {
    $dst = Join-Path $workDir $name
    Copy-Item -LiteralPath "$env:SystemRoot\System32\cmd.exe" -Destination $dst -Force
    $dst
}

<# 单进程触发断言：等待 MON-I-5003（Overlay 命中）与 MON-I-5004（Main 下发）成对出现 #>
function Assert-Triggered([string]$ruleId, [string]$label) {
    $okHit  = Assert-LogMatch ("MON-I-5003 规则 \[$ruleId\] 命中") 15
    $okSent = Assert-LogMatch ("MON-I-5004 规则 \[$ruleId\] 命中") 15
    Check ($label + "（Overlay 命中上报）") $okHit  '日志未见 MON-I-5003'
    Check ($label + "（Main 大字指令下发）") $okSent '日志未见 MON-I-5004'
}

<# 排除断言：目标出现但其规则 id 必不触发 #>
function Assert-NotTriggered([string]$ruleId, [string]$label) {
    $absent = Assert-LogMatch ("MON-I-500[34] 规则 \[$ruleId\] 命中") 6 $false
    Check $label $absent "不应触发的规则 $ruleId 出现在日志中"
}

try {
    Write-Step '0. 环境重置（杀残留进程 + 归档旧日志）'
    Reset-Environment
    if (-not (Test-Path -LiteralPath $mainExe)) { throw "未找到主程序：$mainExe" }

    Write-Step '1. 生成验收配置并启动真实 Main（非冒烟）'
    $testConfig = New-TestConfig
    $mainProc = Start-Process -FilePath $mainExe -ArgumentList @('--config', $testConfig, '--no-autostart') -PassThru
    $ready = Assert-LogMatch 'Overlay 已就绪，ConfigChanged 配置已广播' 20
    Check '主进程启动 + Overlay 就绪（配置链路）' $ready '20s 内未见 Overlay 就绪日志'

    <# AC-04：配置某进程后启动该进程 → 对应大字（进程名精确匹配） #>
    Write-Step '2. AC-04 应用启动触发（进程第一次出现 → 命中）'
    $pAc04 = Start-Process -FilePath (New-TargetExe 'o1688ac04app.exe') -PassThru
    Assert-Triggered 'r-ac04' 'AC-04 进程启动触发'

    <# AC-26：exact / contains / wildcard 三模式 #>
    Write-Step '3. AC-26 匹配模式（exact / contains / wildcard）'
    $pExact = Start-Process -FilePath (New-TargetExe 'o1688exactapp.exe') -PassThru
    Assert-Triggered 'r-exact' 'AC-26 exact 精确匹配'

    $pContains = Start-Process -FilePath (New-TargetExe 'o1688contain-thing.exe') -PassThru
    Assert-Triggered 'r-contains' 'AC-26 contains 包含匹配'

    $pWild = Start-Process -FilePath (New-TargetExe 'o1688wild_999.exe') -PassThru
    Assert-Triggered 'r-wild' 'AC-26 wildcard 通配匹配'

    <# AC-26 排除：无关进程不应命中任何规则 #>
    Write-Step '4. AC-26 排除（无关进程不触发）'
    $pNope = Start-Process -FilePath (New-TargetExe 'o1688nope.exe') -PassThru
    Start-Sleep -Seconds 3
    $excluded = Assert-LogMatch 'MON-I-500[34].*o1688nope' 3 $false
    Check 'AC-26 排除：无关进程未触发' $excluded '无关进程 o1688nope 出现在触发日志中'

    <# AC-26 窗口标题精确匹配。
       注：不用 `cmd /k title X` 构造标题——conhost 控制台窗口经 title 命令设置后
       GetWindowText 返回值带尾随空格（实测 "X " len+1），exact 规则将永不命中（规则值无空格）。
       改用 WinForms 顶层窗口（与第 6 步同法，标题无尾随空格，EnumWindows 可正常捕获）。 #>
    Write-Step '5. AC-26 窗口标题 exact'
    $titleCode = 'Add-Type -AssemblyName System.Windows.Forms; ' +
        '$f=New-Object System.Windows.Forms.Form; ' +
        '$f.Text=''o1688title-exact''; $f.StartPosition=''Manual''; $f.SetBounds(100,100,400,200); ' +
        '$f.Show(); Start-Sleep -Seconds 20; $f.Close()'
    $pTitle = Start-Process powershell -ArgumentList '-NoProfile','-STA','-WindowStyle','Hidden','-Command',$titleCode -PassThru
    Assert-Triggered 'r-title' 'AC-26 窗口标题精确匹配'

    <# AC-05：真实全屏窗口 → includeFullscreen=true 命中、=false 排除 #>
    Write-Step '6. AC-05 全屏触发（borderless 覆盖 rcMonitor）'
    $fsCode = 'Add-Type -AssemblyName System.Windows.Forms; ' +
        '$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds; ' +
        '$f=New-Object System.Windows.Forms.Form; ' +
        '$f.Text=''o1688fullscreen-allow''; $f.FormBorderStyle=''None''; ' +
        '$f.StartPosition=''Manual''; $f.SetBounds(0,0,$b.Width,$b.Height); ' +
        '$f.TopMost=$true; $f.Show(); Start-Sleep -Seconds 30; $f.Close()'
    $pFs = Start-Process powershell -ArgumentList '-NoProfile','-STA','-WindowStyle','Hidden','-Command',$fsCode -PassThru
    Assert-Triggered 'r-full-allow' 'AC-05 全屏窗口命中 includeFullscreen=true'
    Start-Sleep -Seconds 3   # 给同模式 deny 规则留出评估窗口
    Assert-NotTriggered 'r-full-deny' 'AC-05 全屏窗口被 includeFullscreen=false 排除'

    <# M3d 回归：--quit 二次实例转发 → 主进程优雅退出 #>
    Write-Step '7. --quit 二次实例转发 → 优雅退出'
    $pQuit = Start-Process -FilePath $mainExe -ArgumentList '--quit' -PassThru
    $quitForwarded = Assert-LogMatch '二次实例参数转发（--quit）' 8
    Check '二次实例参数转发 --quit' $quitForwarded '未见参数转发日志'

    $quitStarted = Assert-LogMatch '主进程开始优雅退出（原因：UserExit）' 15
    Check '主进程收到 --quit 并启动优雅退出' $quitStarted '未见 UserExit 退出日志'

    Wait-Process -Id $pQuit.Id -Timeout 10 -ErrorAction SilentlyContinue
    $pQuit.Refresh()
    Check '二次实例退出码 0' ($pQuit.HasExited -and $pQuit.ExitCode -eq 0) ("ExitCode=$($pQuit.ExitCode)")

    <# Main 优雅退出完成：注："主进程退出完成"日志在 Logging 进程退出后才发送（MainCoordinator 行 149），
       落不了盘，故改断言主进程真实退出码 0（MainCoordinator 优雅路径 _exitCode=0 → Shutdown(_exitCode)）。 #>
    Wait-Process -Id $mainProc.Id -Timeout 15 -ErrorAction SilentlyContinue
    $mainProc.Refresh()
    Check '主进程优雅退出完成（退出码 0）' ($mainProc.HasExited -and $mainProc.ExitCode -eq 0) ("ExitCode=$($mainProc.ExitCode)")

    <# 无孤儿进程（M1 优雅退出链路回归） #>
    Start-Sleep -Seconds 2
    $orphans = @(Get-Process -Name 'Object1688.Main','Object1688.Overlay','Object1688.Logging','Object1688.Monitor' -ErrorAction SilentlyContinue)
    Check '无孤儿进程残留' ($orphans.Count -eq 0) ("残留: $($orphans.Name -join ', ')")
}
catch {
    $fail++
    Write-Host "  [FATAL] $($_.Exception.Message)" -ForegroundColor Red
    $results.Add("FATAL=$($_.Exception.Message)")
}
finally {
    Write-Step '8. 清理测试进程与临时目录'
    @($pAc04,$pExact,$pContains,$pWild,$pNope,$pTitle,$pFs,$pQuit) |
        Where-Object { $_ } | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
    Get-Process -Name 'Object1688.Main','Object1688.Overlay','Object1688.Logging','Object1688.Monitor' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ''
    Write-Host "==================== 验收汇总 ===================="
    Write-Host ("通过 {0} 项 / 失败 {1} 项" -f $pass, $fail) -ForegroundColor ($(if ($fail -eq 0) {'Green'} else {'Red'}))
    exit $(if ($fail -eq 0) { 0 } else { 1 })
}