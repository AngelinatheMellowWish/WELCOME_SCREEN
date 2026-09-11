#requires -Version 5.1
<#
    M6 实机人工验收脚本（AC-24 多显示器 / AC-42 混合 DPI / AC-74 旋转屏）。
    这三项依赖真实显示硬件，无法自动化判定，本脚本负责：
      1) 枚举当前显示器配置并给出前置条件是否满足；
      2) 逐步引导操作（含自动向指定屏触发大字）；
      3) 收集 PASS/FAIL/SKIP 并写出报告到 log/m6-manual-<时间戳>.txt。

    用法：
      powershell -NoProfile -ExecutionPolicy Bypass -File build/verify-m6-manual.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File build/verify-m6-manual.ps1 -Check   # 仅枚举显示器后退出
#>
param([switch]$Check)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

# 解析 .NET 8（仅用户级安装时 apphost 需要 DOTNET_ROOT）
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

$root    = Split-Path -Parent $PSScriptRoot
$mainBin = Join-Path $root 'src\Object1688.Main\bin\Debug\net8.0-windows'
$mainExe = Join-Path $mainBin 'Object1688.Main.exe'
$logDir  = Join-Path $mainBin 'log'
$workDir = Join-Path $env:TEMP ("o1688-manual-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$cfgFile = Join-Path $workDir 'config.manual.json'
$report  = Join-Path $logDir ("m6-manual-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".txt")
$results = [System.Collections.Generic.List[string]]::new()

Add-Type -AssemblyName System.Windows.Forms
$screens = @([System.Windows.Forms.Screen]::AllScreens)

function Show-Displays {
    Write-Host "检测到显示器配置：" -ForegroundColor Cyan
    $i = 1
    foreach ($s in $screens) {
        Write-Host ("  [$i] " + $s.DeviceName + "  primary=" + $s.Primary + "  分辨率=" + $s.Bounds.Width + "x" + $s.Bounds.Height)
        $i++
    }

    Write-Host "  提示：可在「设置 → 系统 → 显示」中查看各屏缩放比例（%）。" -ForegroundColor DarkGray
}

function Ask-Result([string]$name, [string]$question) {
    while ($true) {
        $ans = (Read-Host ($question + "  [P]通过 / [F]失败 / [S]跳过")).Trim().ToUpperInvariant()
        if ($ans -in @('P', 'F', 'S')) { break }
    }

    $map = @{ 'P' = 'PASS'; 'F' = 'FAIL'; 'S' = 'SKIP' }
    $r = $map[$ans]
    $results.Add("$name=$r")
    $color = if ($r -eq 'PASS') { 'Green' } elseif ($r -eq 'FAIL') { 'Red' } else { 'Yellow' }
    Write-Host ("  -> $name = $r") -ForegroundColor $color
}

function Trigger-Banner([string]$text, [string]$screen) {
    try {
        & $mainExe --control banner --text $text --screen $screen | Out-Null
        Write-Host ("  已触发大字（targetScreen=$screen）：$text") -ForegroundColor Gray
    }
    catch {
        Write-Host ("  触发失败：" + $_.Exception.Message) -ForegroundColor Red
    }
}

function New-Config {
    $config = [ordered]@{
        schemaVersion = 1
        global = [ordered]@{
            defaultFontSize = 120; defaultPosition = 'center'; defaultOutlineColor = '#000000'
            defaultOutlineWidth = 4; targetScreen = 'primary'; monitorPollIntervalMs = 1500
            dedupeWindowSeconds = 10; logLevel = 'INFO'; logMaxSizeMB = 5; logRetainCount = 10
            dpiAwareness = 'per-monitor-v2'
        }
        welcome = [ordered]@{ enabled = $false; displayLines = @([ordered]@{ text = 'm6'; fontSize = 96 }) }
        manual  = [ordered]@{ enabled = $false; displayLines = @([ordered]@{ text = 'm6'; fontSize = 96 }) }
        rules   = @()
        dnd     = [ordered]@{ paused = $false; scheduleEnabled = $false; schedule = @() }
        sound   = [ordered]@{ enabled = $false; source = 'system'; customPath = ''; volume = 80 }
        autostart = $false
        firstRun  = $false
    }

    New-Item -ItemType Directory -Path $workDir -Force | Out-Null
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $cfgFile -Encoding UTF8
}

function Start-App {
    New-Config
    $p = Start-Process -FilePath $mainExe -ArgumentList @('--no-autostart', '--config', $cfgFile) -PassThru
    Start-Sleep -Seconds 3
    return $p
}

Write-Host "==================== M6 实机人工验收（AC-24/42/74）====================" -ForegroundColor Cyan
Show-Displays
if ($Check) {
    Write-Host "check ok" -ForegroundColor Green
    exit 0
}

if (-not (Test-Path -LiteralPath $mainExe)) {
    Write-Host ("未找到主程序（请先构建）：" + $mainExe) -ForegroundColor Red
    exit 1
}

$proc = Start-App
try {
    Write-Host ""
    Write-Host "== AC-24 多显示器目标屏 ==" -ForegroundColor Cyan
    if ($screens.Count -lt 2) {
        Write-Host "  当前仅 1 个显示器，无法验证（需 ≥2 屏）。" -ForegroundColor Yellow
        $results.Add("AC-24=SKIP(仅1屏)")
    }
    else {
        Write-Host "  将向 targetScreen=2（第 2 块显示器）触发大字。"
        Write-Host "  预期：大字完整出现在第 2 块显示器上，且主屏不出现。" -ForegroundColor Gray
        Read-Host "  按回车触发大字" | Out-Null
        Trigger-Banner "AC-24 多显示器" "2"
        Ask-Result "AC-24 多显示器目标屏" "大字是否显示在指定的副屏上？"
    }

    Write-Host ""
    Write-Host "== AC-42 混合 DPI ==" -ForegroundColor Cyan
    if ($screens.Count -lt 2) {
        Write-Host "  当前仅 1 个显示器，无法验证（需 ≥2 屏且缩放不同）。" -ForegroundColor Yellow
        $results.Add("AC-42=SKIP(仅1屏)")
    }
    else {
        Write-Host "  前置：将两块显示器设为不同缩放（如 100% 与 150%），并把配置/性能窗口拖到另一块屏。"
        Write-Host "  预期：文字清晰、无模糊/错位；窗口跨屏拖动后布局正常。" -ForegroundColor Gray
        Read-Host "  按回车触发大字" | Out-Null
        Trigger-Banner "AC-42 混合 DPI" "primary"
        Ask-Result "AC-42 混合 DPI" "跨不同缩放显示器时文字/窗口是否清晰、无错位？"
    }

    Write-Host ""
    Write-Host "== AC-74 旋转屏 ==" -ForegroundColor Cyan
    Write-Host "  前置：在「设置 → 系统 → 显示 → 显示方向」把某块屏旋转为「纵向」（或反之）。"
    Write-Host "  预期：该屏上大字排版/换行正确、不裁切、不溢出。" -ForegroundColor Gray
    Read-Host "  按回车触发大字（若已旋转）" | Out-Null
    Trigger-Banner "AC-74 旋转屏" "primary"
    Ask-Result "AC-74 旋转屏" "旋转屏上大字排版是否正确（不裁切/不溢出）？"
}
finally {
    & $mainExe --control quit | Out-Null
    Start-Sleep -Seconds 2
    Get-Process -Name 'Object1688.Main', 'Object1688.Overlay', 'Object1688.Logging', 'Object1688.Monitor' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "==================== 人工验收汇总 ====================" -ForegroundColor Cyan
    foreach ($r in $results) {
        $color = if ($r -match '=PASS') { 'Green' } elseif ($r -match '=FAIL') { 'Red' } else { 'Yellow' }
        Write-Host ("  " + $r) -ForegroundColor $color
    }

    try {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        $lines = [System.Collections.Generic.List[string]]::new()
        $lines.Add("M6 manual acceptance report " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        $lines.Add("host=" + $env:COMPUTERNAME + " os=" + [System.Environment]::OSVersion.VersionString)
        $lines.Add("screens=" + $screens.Count)
        $i = 1
        foreach ($s in $screens) {
            $lines.Add("  [$i] " + $s.DeviceName + " primary=" + $s.Primary + " " + $s.Bounds.Width + "x" + $s.Bounds.Height)
            $i++
        }

        foreach ($r in $results) { $lines.Add("  " + $r) }
        [System.IO.File]::WriteAllLines($report, $lines, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host ("report: " + $report) -ForegroundColor Gray
    }
    catch {
        Write-Host ("report write failed: " + $_.Exception.Message) -ForegroundColor Red
    }
}
