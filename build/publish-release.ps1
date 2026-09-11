<#
.SYNOPSIS
    Publish a Release build of Object1688 (architecture 8.1 / 9.1 / M6 packaging).
.DESCRIPTION
    Builds Release (warnaserror), runs the resx key gate, then publishes the five
    process exes (Main + Overlay + Monitor + Logging + ConfigUI) self-contained,
    single-file, win-x64 into dist/publish, and copies LICENSE, THIRD_PARTY_NOTICES,
    config.default.json and docs(.txt) alongside. Pure ASCII on purpose (PowerShell
    5.1 no-BOM handling; see sync-docs.ps1 history).
.PARAMETER Runtime
    Target RID (default win-x64).
.PARAMETER SelfContained
    Include .NET runtime (default true).
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build\publish-release.ps1
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [switch]$SelfContained = $true
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'src\Object1688.sln'
$publishDir = Join-Path $root "dist\publish-$Runtime"
$childProjects = @(
    'Object1688.Overlay',
    'Object1688.Monitor',
    'Object1688.Logging',
    'Object1688.ConfigUI'
)

# Locate dotnet: prefer DOTNET_ROOT (opencode/PowerShell sessions set it), else on PATH
if ($env:DOTNET_ROOT -and (Test-Path (Join-Path $env:DOTNET_ROOT 'dotnet.exe'))) {
    $dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
} else {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
}
if (-not $dotnet) { throw 'dotnet not found: set DOTNET_ROOT or add dotnet to PATH' }
Write-Host "Using dotnet: $dotnet"

Write-Host "== Restore (runtime $Runtime) =="
& $dotnet restore $sln -r $Runtime
if ($LASTEXITCODE -ne 0) { throw 'restore failed' }

Write-Host "== Build Release (warnaserror) =="
& $dotnet build $sln -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

Write-Host "== ResX key gate =="
& (Join-Path $PSScriptRoot 'check-resx-keys.ps1')
if ($LASTEXITCODE -ne 0) { throw 'resx gate failed' }

Write-Host "== Clean previous publish output =="
if (Test-Path $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue }

Write-Host "== Publish Main (self-contained single-file $Runtime) =="
# Do NOT use --no-build: publish must rebuild to produce RID singlefilehost/apphost (GenerateBundle depends on it).
& $dotnet publish (Join-Path $root 'src\Object1688.Main\Object1688.Main.csproj') -c Release -r $Runtime -p:PublishSingleFile=true -p:SelfContained=$SelfContained --self-contained $SelfContained -o (Join-Path $publishDir 'Main') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Main publish failed' }

Write-Host "== Publish child exes =="
foreach ($p in $childProjects) {
    $proj = Join-Path $root "src\$p\$p.csproj"
    & $dotnet publish $proj -c Release -r $Runtime -p:PublishSingleFile=true -p:SelfContained=$SelfContained --self-contained $SelfContained -o (Join-Path $publishDir 'Main') --nologo
    if ($LASTEXITCODE -ne 0) { throw "$p publish failed" }
}

Write-Host "== Copy license / notices / config template / docs =="
Copy-Item (Join-Path $root 'LICENSE')  (Join-Path $publishDir 'Main\LICENSE')  -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES') (Join-Path $publishDir 'Main\THIRD_PARTY_NOTICES') -Force -ErrorAction SilentlyContinue
# Template must land in config\ (ConfigLoader.DefaultTemplatePath = BaseDirectory/config/config.default.json)
$configDir = Join-Path $publishDir 'Main\config'
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
Copy-Item (Join-Path $root 'config\config.default.json') (Join-Path $configDir 'config.default.json') -Force -ErrorAction SilentlyContinue

# Ship the uninstall helper. Double-click build\uninstall.cmd (a .ps1 default-opens in Notepad on Win11).
$buildDir = Join-Path $publishDir 'Main\build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
Copy-Item (Join-Path $root 'build\uninstall.ps1') (Join-Path $buildDir 'uninstall.ps1') -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'build\uninstall.cmd') (Join-Path $buildDir 'uninstall.cmd') -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'build\quit.cmd') (Join-Path $buildDir 'quit.cmd') -Force -ErrorAction SilentlyContinue

Write-Host "== Done =="
Write-Host "Publish output: $publishDir\Main"
Write-Host "Run Object1688.Main.exe from that directory (launches children side-by-side)."
