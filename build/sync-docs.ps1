# sync-docs.ps1 - Sync all .md files in docs/ to .txt backups.
# Rule: every .md gets a matching .txt, generated automatically, never hand-copied.
# Usage: pwsh ./build/sync-docs.ps1   (run from project root)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$docsDir = Join-Path $projectRoot "docs"

if (-not (Test-Path -LiteralPath $docsDir)) {
    Write-Host "ERROR: docs/ directory not found: $docsDir"
    exit 1
}

$synced = 0
Get-ChildItem -LiteralPath $docsDir -Filter "*.md" -File | ForEach-Object {
    $mdFile  = $_.FullName
    $txtFile = [System.IO.Path]::ChangeExtension($mdFile, ".txt")
    Copy-Item -LiteralPath $mdFile -Destination $txtFile -Force
    Write-Host "Synced: $($_.Name) -> $([System.IO.Path]::GetFileName($txtFile))"
    $synced++
}

Write-Host "Done: synced $synced .md files to .txt backups."
if ($synced -eq 0) {
    Write-Host "WARNING: No .md files found in docs/."
}