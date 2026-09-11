#requires -Version 5.1
<#
    M6 acceptance harness (AC-40/67/82/85/97/98 + hardware checklist for AC-24/42/74).
    Automates the machine-checkable parts of the M6 environment matrix and prints the
    manual checklist for the hardware-dependent items (multi-monitor / mixed DPI / rotation).

      AC-82  degraded startup self-check: missing --config -> GEN-W-9004, main keeps running
      AC-67  startup readiness <= 3s ; idle memory reported vs <= 80MB target
      AC-40  RuleMatcherBench 500-rule match budget < 5ms/poll
      AC-85  benchmark runnable (same run as AC-40)
      AC-97/98  control interface ping/status reachable + graceful quit
      AC-24/42/74/82  printed manual checklist (needs real hardware / fault injection)

    Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File build/verify-m6-acceptance.ps1
    Exit:   0 = all automated checks pass ; 1 = one or more failed.
#>
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = 'Stop'

# Resolve .NET 8 (apphost cannot locate it when only a user-local install exists).
$env:DOTNET_ROOT = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"

$root      = Split-Path -Parent $PSScriptRoot
$mainBin   = Join-Path $root 'src\Object1688.Main\bin\Debug\net8.0-windows'
$mainExe   = Join-Path $mainBin 'Object1688.Main.exe'
$logDir    = Join-Path $mainBin 'log'
$logFile   = Join-Path $logDir 'app.log'
$prevLog   = Join-Path $logDir 'app.prev.log'
$testsProj = Join-Path $root 'tests\Object1688.Tests\Object1688.Tests.csproj'
$dotnet    = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
$workDir   = Join-Path $env:TEMP ("o1688-m6-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$cfgFile   = Join-Path $workDir 'config.m6.json'
$missingCfg= Join-Path $workDir 'does-not-exist.json'
$benchOut  = Join-Path $workDir 'bench.txt'
$reportFile= Join-Path $logDir ("m6-acceptance-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + ".txt")

$procNames = @('Object1688.Main','Object1688.Overlay','Object1688.Logging','Object1688.Monitor','Object1688.ConfigUI')
$pass = 0; $fail = 0
$results = [System.Collections.Generic.List[string]]::new()
$manual  = [System.Collections.Generic.List[string]]::new()

function Write-Step([string]$m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Check([string]$name, [bool]$ok, [string]$detail) {
    if ($ok) { $script:pass++; Write-Host "  [PASS] $name" -ForegroundColor Green }
    else     { $script:fail++; Write-Host "  [FAIL] $name : $detail" -ForegroundColor Red }
    $script:results.Add("$name=$ok")
}
function Report([string]$name, [string]$value) {
    Write-Host "  [INFO] $name = $value" -ForegroundColor Yellow
    $script:results.Add("$name=$value")
}
function Get-LogText {
    if (Test-Path -LiteralPath $logFile) { Get-Content -LiteralPath $logFile -Raw -Encoding UTF8 } else { '' }
}
function Wait-LogMatch([string]$pattern, [int]$timeoutSec = 20, [bool]$mustExist = $true) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    do {
        $c = Get-LogText
        if ($mustExist -and $c -match $pattern) { return $true }
        if (-not $mustExist -and $c -match $pattern) { return $false }
        Start-Sleep -Milliseconds 150
    } while ((Get-Date) -lt $deadline)
    return -not $mustExist
}
function Stop-All {
    Get-Process -Name $procNames -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}
function Reset-Environment {
    Stop-All
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    if (Test-Path -LiteralPath $logFile) { Move-Item -LiteralPath $logFile -Destination $prevLog -Force -ErrorAction SilentlyContinue }
}
function Start-Main([string[]]$extra) {
    $a = @('--no-autostart') + $extra
    Start-Process -FilePath $mainExe -ArgumentList $a -PassThru
}
function Quit-Main([System.Diagnostics.Process]$p) {
    & $mainExe --control quit | Out-Null
    Wait-Process -Id $p.Id -Timeout 15 -ErrorAction SilentlyContinue
}
function Parse-Ts([string]$line) {
    if ($line -match '\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})\]') {
        return [datetime]::ParseExact($Matches[1], 'yyyy-MM-dd HH:mm:ss.fff', $null)
    }
    return $null
}
function Get-ReadyLine {
    $lines = if (Test-Path -LiteralPath $logFile) { Get-Content -LiteralPath $logFile -Encoding UTF8 } else { @() }
    ($lines | Where-Object { $_ -match 'Overlay .*ConfigChanged' } | Select-Object -Last 1)
}
function Get-IdleMemoryMB {
    $sum = 0.0
    foreach ($n in $procNames) {
        Get-Process -Name $n -ErrorAction SilentlyContinue | ForEach-Object { $sum += $_.WorkingSet64 / 1024.0 / 1024.0 }
    }
    [int]$sum
}
function New-Config {
    $config = [ordered]@{
        schemaVersion = 1
        global = [ordered]@{
            defaultFontSize = 96; defaultPosition = 'center'; defaultOutlineColor = '#000000'
            defaultOutlineWidth = 0; targetScreen = 'primary'; monitorPollIntervalMs = 1500
            dedupeWindowSeconds = 10; logLevel = 'INFO'; logMaxSizeMB = 5; logRetainCount = 10
            dpiAwareness = 'per-monitor-v2'
        }
        welcome = [ordered]@{ enabled = $false; displayLines = @([ordered]@{ text = 'm6-welcome'; fontSize = 96 }) }
        manual  = [ordered]@{ enabled = $false; displayLines = @([ordered]@{ text = 'm6-manual'; fontSize = 96 }) }
        rules   = @()
        dnd     = [ordered]@{ paused = $false; scheduleEnabled = $false; schedule = @() }
        sound   = [ordered]@{ enabled = $false; source = 'system'; customPath = ''; volume = 80 }
        autostart = $false
        firstRun  = $false
    }
    New-Item -ItemType Directory -Path $workDir -Force | Out-Null
    $config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $cfgFile -Encoding UTF8
    $cfgFile
}

$mainProc = $null
try {
    Write-Step '0. Reset environment'
    Reset-Environment
    New-Item -ItemType Directory -Path $workDir -Force | Out-Null

    Write-Step '1. Build artifacts present (5 executables)'
    $exes = @('Object1688.Main','Object1688.Overlay','Object1688.Logging','Object1688.Monitor','Object1688.ConfigUI') |
        ForEach-Object { Join-Path $mainBin "$_.exe" }
    $missing = @($exes | Where-Object { -not (Test-Path -LiteralPath $_) })
    Check 'M6-01 five executables present' ($missing.Count -eq 0) ("missing: " + ($missing -join ', '))
    if ($missing.Count -gt 0) { throw "build artifacts missing; run 'dotnet build src/Object1688.sln' first" }

    # AC-82: fault injection (missing config) -> degraded warning, process keeps running.
    Write-Step '2. AC-82 degraded startup self-check (missing config -> GEN-W-9004)'
    $p82 = Start-Main @('--config', $missingCfg)
    $degraded = Wait-LogMatch 'GEN-W-9004' 20
    Check 'AC-82 degraded self-check logged (GEN-W-9004)' $degraded 'no GEN-W-9004 in app.log'
    Start-Sleep -Seconds 2
    $p82.Refresh()
    Check 'AC-82 degraded mode keeps running (no exit)' (-not $p82.HasExited) ("Exited=$($p82.HasExited) code=$($p82.ExitCode)")
    Quit-Main $p82
    $p82.Refresh()
    Check 'AC-82 degraded main exits 0 on quit' ($p82.HasExited -and $p82.ExitCode -eq 0) ("ExitCode=$($p82.ExitCode)")

    # AC-67 startup readiness + idle memory (valid config).
    Write-Step '3. AC-67 startup readiness <= 3s'
    $cfg = New-Config
    if (Test-Path -LiteralPath $logFile) { Move-Item -LiteralPath $logFile -Destination $prevLog -Force -ErrorAction SilentlyContinue }
    $mainProc = Start-Main @('--config', $cfg)
    $ready = Wait-LogMatch 'Overlay .*ConfigChanged' 20
    Check 'AC-67 overlay ready within 20s (pipeline up)' $ready 'no Overlay-ready log'
    $readyLine = Get-ReadyLine
    $ts = Parse-Ts $readyLine
    if ($ts) {
        $startupMs = [int](($ts - $mainProc.StartTime).TotalMilliseconds)
        Check 'AC-67 startup readiness <= 3000ms' ($startupMs -le 3000 -and $startupMs -ge 0) ("startup=${startupMs}ms")
    } else {
        Check 'AC-67 startup readiness measurable' $false 'could not parse Overlay-ready timestamp'
    }

    Write-Step '4. AC-67 idle memory (report vs <= 80MB target)'
    Start-Sleep -Seconds 8
    $idleMB = Get-IdleMemoryMB
    Report 'AC-67 idle memory (4 processes, MB)' "$idleMB"
    if ($idleMB -gt 80) {
        $manual.Add("AC-67 memory: measured ${idleMB}MB > 80MB target (optimize or review target)")
    }

    Write-Step '5. AC-97/98 control interface (ping / status) while running'
    $ping = (& $mainExe --control ping | Out-String)
    Check 'AC-97 control ping ok' ($ping -match '"ok"\s*:\s*true') ("resp=" + $ping.Trim())
    $status = (& $mainExe --control status | Out-String)
    Check 'AC-97 control status ok' ($status -match '"ok"\s*:\s*true') ("resp=" + $status.Trim())

    Write-Step '5b. AC-67 perf window open <= 1s'
    $swOpen = [System.Diagnostics.Stopwatch]::StartNew()
    & $mainExe --control perf | Out-Null
    $opened = Wait-LogMatch 'AC-10/AC-67' 8
    $openMs = $swOpen.ElapsedMilliseconds
    Check 'AC-67 perf window open <= 1000ms' ($opened -and $openMs -le 1000) ("opened=$opened elapsed=${openMs}ms")
    Report 'AC-67 perf window open (ms)' "$openMs"

    Write-Step '5c. AC-67 animation fps >= 60 (via FrameRate report)'
    & $mainExe --control banner --text 'M6 fps probe' | Out-Null
    Start-Sleep -Seconds 3
    $statusFps = (& $mainExe --control status | Out-String)
    if ($statusFps -match '"fps"\s*:\s*(-?[0-9]+(?:\.[0-9]+)?)') {
        $fps = [double]$Matches[1]
        Check 'AC-67 animation fps >= 60' ($fps -ge 60) ("fps=$fps")
        Report 'AC-67 animation fps' "$fps"
    } else {
        Check 'AC-67 fps reported in status' $false ("status=" + $statusFps.Trim())
    }

    Write-Step '6. graceful quit + no orphan processes'
    Quit-Main $mainProc
    $mainProc.Refresh()
    Check 'Main graceful exit code 0' ($mainProc.HasExited -and $mainProc.ExitCode -eq 0) ("ExitCode=$($mainProc.ExitCode)")
    Start-Sleep -Seconds 2
    $orphans = @(Get-Process -Name $procNames -ErrorAction SilentlyContinue)
    Check 'no orphan processes' ($orphans.Count -eq 0) ("leftover: " + ($orphans.ProcessName -join ', '))

    # AC-40 / AC-85: 500-rule match budget (guarded by OBJECT1688_PERF_BENCH).
    Write-Step '7. AC-40/AC-85 500-rule match benchmark (< 5ms/poll)'
    $env:OBJECT1688_PERF_BENCH = '1'
    & $dotnet test $testsProj --filter 'FullyQualifiedName~RuleMatcherBench' --logger 'console;verbosity=detailed' *> $benchOut
    $env:OBJECT1688_PERF_BENCH = ''
    $benchText = if (Test-Path -LiteralPath $benchOut) { Get-Content -LiteralPath $benchOut -Raw -Encoding UTF8 } else { '' }
    if ($benchText -match '\[PERF\][^\r\n]*?([0-9]+(?:\.[0-9]+)?)\s*ms/poll') {
        $ms = [double]$Matches[1]
        Check 'AC-40 500-rule match budget < 5ms' ($ms -lt 5.0) ("measured=${ms}ms/poll")
    } else {
        Check 'AC-40 benchmark produced a measurement' $false 'no [PERF] line found'
    }
}
catch {
    $fail++
    Write-Host "  [FATAL] $($_.Exception.Message)" -ForegroundColor Red
    $results.Add("FATAL=$($_.Exception.Message)")
}
finally {
    Write-Step '8. Cleanup'
    if ($mainProc -and -not $mainProc.HasExited) { Stop-Process -Id $mainProc.Id -Force -ErrorAction SilentlyContinue }
    Stop-All
    Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue

    $manual.Add('Run build/verify-m6-manual.ps1 on the target machine for guided AC-24/42/74 verification')
    $manual.Add('AC-24 multi-monitor: set a rule targetScreen=secondary -> big text must show on the secondary display')
    $manual.Add('AC-42 mixed DPI: drag across monitors with different scaling -> text stays sharp/aligned')
    $manual.Add('AC-74 rotation: rotate a display to portrait -> layout/wrapping stays correct')
    $manual.Add('AC-82 fault injection: remove font asset / occupy hotkey / occupy pipe -> degraded prompt, no crash')

    Write-Host ''
    Write-Host '==================== M6 ACCEPTANCE SUMMARY ====================' -ForegroundColor Cyan
    Write-Host ("automated: {0} pass / {1} fail" -f $pass, $fail) -ForegroundColor ($(if ($fail -eq 0) { 'Green' } else { 'Red' }))
    Write-Host 'manual checklist (hardware-dependent):' -ForegroundColor Yellow
    foreach ($m in $manual) { Write-Host ("  - " + $m) -ForegroundColor Yellow }

    try {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        $lines = [System.Collections.Generic.List[string]]::new()
        $lines.Add("M6 acceptance report " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        $lines.Add("host=" + $env:COMPUTERNAME + " os=" + [System.Environment]::OSVersion.VersionString)
        $lines.Add("automated pass=$pass fail=$fail")
        foreach ($r in $results) { $lines.Add("  " + $r) }
        $lines.Add('manual checklist:')
        foreach ($m in $manual) { $lines.Add("  - " + $m) }
        [System.IO.File]::WriteAllLines($reportFile, $lines, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host ("report: " + $reportFile) -ForegroundColor Gray
    }
    catch { Write-Host ("report write failed: " + $_.Exception.Message) -ForegroundColor Red }

    exit $(if ($fail -eq 0) { 0 } else { 1 })
}
