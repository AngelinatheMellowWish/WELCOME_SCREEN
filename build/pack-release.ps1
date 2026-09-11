<#
.SYNOPSIS
    Pack the published release into a clean, user-downloadable zip (M6 packaging).
.DESCRIPTION
    Takes the output of publish-release.ps1 (dist/publish-<rid>/Main) and produces
    dist/Object1688-v<Version>-<rid>.zip, EXCLUDING runtime/debug clutter:
      - log\            (runtime logs)
      - *.pdb           (debug symbols)
      - *.xml           (XML doc comments)
    The zip root contains Object1688.Main.exe + the 4 child exes + config\ + build\ +
    LICENSE + THIRD_PARTY_NOTICES, so users just extract and double-click Main.exe.
.PARAMETER Version
    Version string used in the zip file name. If omitted, derived from the built
    Object1688.Main.exe FileVersion (e.g. 0.2.1.0 -> 0.2.1).
.PARAMETER Runtime
    Target RID matching the publish output (default win-x64).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\pack-release.ps1
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "dist\publish-$Runtime\Main"

if (-not (Test-Path -LiteralPath $src)) {
    throw "Publish output not found: $src (run build\publish-release.ps1 first)"
}

if (-not $Version) {
    $mainExe = Join-Path $src 'Object1688.Main.exe'
    if (Test-Path -LiteralPath $mainExe) {
        $fv = (Get-Item -LiteralPath $mainExe).VersionInfo.FileVersion
        if ($fv) { $Version = ($fv -split '\.')[0..2] -join '.' }
    }
    if (-not $Version) { $Version = '0.0.0' }
}

$out = Join-Path $root "dist\Object1688-v$Version-$Runtime.zip"

if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }

$stage = Join-Path $env:TEMP ("o1688-pack-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    # Copy everything except the runtime log\ folder
    Get-ChildItem -LiteralPath $src -Force | Where-Object { $_.Name -ne 'log' } |
        Copy-Item -Destination $stage -Recurse -Force

    # Drop debug artifacts
    Get-ChildItem -LiteralPath $stage -Recurse -File |
        Where-Object { $_.Extension -in '.pdb', '.xml' } |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $stage, $out, [System.IO.Compression.CompressionLevel]::Optimal, $false)
}
finally {
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
}

$sizeMB = [math]::Round((Get-Item -LiteralPath $out).Length / 1MB, 1)
Write-Host "Release zip: $out ($sizeMB MB)"
