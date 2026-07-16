param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"

$PatchRoot = Split-Path -Parent $PSScriptRoot
$SourceRoot = Join-Path $PatchRoot "InteriorDesignWeb"

if (!(Test-Path $ProjectRoot)) {
    throw "ProjectRoot not found: $ProjectRoot"
}

$backupDir = Join-Path $ProjectRoot ("backup_ai_workflow_v3_fix_" + (Get-Date -Format "yyyyMMdd_HHmmss"))
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

$files = @(
    "Services\AI\IAIJobService.cs",
    "Services\AI\AIJobService.cs"
)

foreach ($file in $files) {
    $src = Join-Path $SourceRoot $file
    $dst = Join-Path $ProjectRoot $file
    $backup = Join-Path $backupDir $file

    if (!(Test-Path $src)) {
        throw "Patch source missing: $src"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
    if (Test-Path $dst) {
        Copy-Item $dst $backup -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null
    Copy-Item $src $dst -Force
    Write-Host "Updated: $file"
}

Write-Host "Done. Backup saved to: $backupDir"
Write-Host "Next: dotnet clean; dotnet build"
