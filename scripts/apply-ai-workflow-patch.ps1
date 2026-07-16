param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectRoot
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $ProjectRoot)) {
    throw "ProjectRoot 不存在：$ProjectRoot"
}

$PatchRoot = Split-Path -Parent $PSScriptRoot
$SourceRoot = Join-Path $PatchRoot "InteriorDesignWeb"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupRoot = Join-Path $ProjectRoot "_backup_ai_workflow_patch_$Timestamp"

New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

Get-ChildItem -Path $SourceRoot -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($SourceRoot.Length).TrimStart('\','/')
    $target = Join-Path $ProjectRoot $relative
    $backup = Join-Path $BackupRoot $relative

    if (Test-Path $target) {
        New-Item -ItemType Directory -Force -Path (Split-Path $backup) | Out-Null
        Copy-Item $target $backup -Force
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item $_.FullName $target -Force
}

Write-Host "补丁应用完成。备份目录：$BackupRoot" -ForegroundColor Green
Write-Host "下一步：执行 database/20260706_ai_workflow_integration.sql，然后 dotnet build。" -ForegroundColor Yellow
