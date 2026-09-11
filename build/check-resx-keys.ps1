<#
.SYNOPSIS
    AC-75 i18n resource-key consistency gate: verifies Strings.zh-Hans.resx and
    Strings.en.resx have identical <data name> key sets.
.DESCRIPTION
    dev-standards 4.5: zh-Hans and en key sets MUST be identical.
    Used locally and by GitHub Actions (ci.yml). exit 0 = consistent; exit 1 = mismatch (CI blocks).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\check-resx-keys.ps1
#>
[CmdletBinding()]
param(
    # zh resource path, relative to script dir
    [string]$ZhPath = '..\src\Object1688.Shared\Resources\Strings.zh-Hans.resx',
    # en resource path, relative to script dir
    [string]$EnPath = '..\src\Object1688.Shared\Resources\Strings.en.resx'
)

$ErrorActionPreference = 'Stop'

# Script dir (robust across -File / dot-source hosts)
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot }
             elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
             else { (Get-Location).Path }

function Get-ResxKeys {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Resource file not found: $Path"
    }
    [xml]$xml = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $keys = @($xml.root.data | ForEach-Object { $_.name })
    return [System.Collections.Generic.HashSet[string]]::new(
        [string[]]$keys, [System.StringComparer]::Ordinal)
}

$zh = Get-ResxKeys -Path (Join-Path $scriptDir $ZhPath)
$en = Get-ResxKeys -Path (Join-Path $scriptDir $EnPath)

$onlyZh = @($zh | Where-Object { -not $en.Contains($_) } | Sort-Object)
$onlyEn = @($en | Where-Object { -not $zh.Contains($_) } | Sort-Object)

$failed = $false
if ($onlyZh.Count -gt 0) {
    $failed = $true
    Write-Host "[FAIL] zh-Hans-only keys (missing in en): $($onlyZh -join ', ')"
}
if ($onlyEn.Count -gt 0) {
    $failed = $true
    Write-Host "[FAIL] en-only keys (missing in zh-Hans): $($onlyEn -join ', ')"
}

if ($failed) {
    Write-Host "AC-75 key-consistency gate FAILED ($($zh.Count)/$($en.Count) keys)."
    exit 1
}

Write-Host "[PASS] AC-75 key-consistency gate OK: $($zh.Count) keys identical."
exit 0
