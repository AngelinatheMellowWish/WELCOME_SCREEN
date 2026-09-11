<#
.SYNOPSIS
    Uninstall helper for Object1688 (architecture 8.5 / F-72).
.DESCRIPTION
    Removes the HKCU autostart Run value, deletes the runtime config directory
    (%APPDATA%\Object1688), and optionally the log directory. Pure ASCII on purpose
    (PowerShell 5.1 reads no-BOM .ps1 as ANSI; Chinese comments break parsing).
.PARAMETER AlsoCleanLogs
    Also delete log/ crash dumps and exports under the app base directory.
.PARAMETER ConfigPath
    Override the config path (default: %APPDATA%\Object1688\config.json).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\uninstall.ps1 -AlsoCleanLogs
#>
[CmdletBinding()]
param(
    [switch]$AlsoCleanLogs,
    [string]$ConfigPath = (Join-Path $env:APPDATA 'Object1688\config.json')
)

$ErrorActionPreference = 'Continue'

# 1) Remove autostart Run value (F-24 default on)
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$valueName = 'Object1688'
try {
    if (Get-ItemProperty -Path $runKey -Name $valueName -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $runKey -Name $valueName -ErrorAction Stop
        Write-Host "[OK] Removed autostart value '$valueName' from $runKey"
    } else {
        Write-Host "[SKIP] No autostart value '$valueName' present"
    }
} catch {
    Write-Warning "Failed to remove autostart value: $($_.Exception.Message)"
}

# 2) Delete runtime config directory (default %APPDATA%\Object1688)
$configDir = Split-Path -Parent $ConfigPath
if (-not $configDir) { $configDir = Join-Path $env:APPDATA 'Object1688' }
if (Test-Path -LiteralPath $configDir) {
    try {
        Remove-Item -LiteralPath $configDir -Recurse -Force -ErrorAction Stop
        Write-Host "[OK] Removed runtime config directory: $configDir"
    } catch {
        Write-Warning "Failed to remove $configDir (files may be in use): $($_.Exception.Message)"
    }
} else {
    Write-Host "[SKIP] Runtime config directory not present: $configDir"
}

# 3) Optional: clean log/ (crash dumps + exports) under app base directory
if ($AlsoCleanLogs) {
    $base = $PSScriptRoot | Split-Path -Parent
    $logDir = Join-Path $base 'log'
    if (Test-Path -LiteralPath $logDir) {
        try {
            Remove-Item -LiteralPath $logDir -Recurse -Force -ErrorAction Stop
            Write-Host "[OK] Removed log directory: $logDir"
        } catch {
            Write-Warning "Failed to remove ${logDir}: $($_.Exception.Message)"
        }
    } else {
        Write-Host "[SKIP] Log directory not present: $logDir"
    }
}

Write-Host "Uninstall helper finished. Manual steps are documented in MANUAL (remove app files / shortcuts)."
