param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $PublishPath).Path

$required = @(
    'InteriorDesignWeb.dll',
    'appsettings.json',
    'wwwroot\dist\index.html',
    'workflow\api_grok_image_edit.json'
)

$forbidden = @(
    'InteriorDesignWeb.pdb',
    'appsettings.Development.json',
    'wwwroot\src',
    'wwwroot\.env.production',
    'wwwroot\package.json',
    'wwwroot\vite.config.ts'
)

foreach ($relativePath in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relativePath))) {
        throw "Required publish artifact is missing: $relativePath"
    }
}

foreach ($relativePath in $forbidden) {
    if (Test-Path -LiteralPath (Join-Path $root $relativePath)) {
        throw "Development file leaked into publish output: $relativePath"
    }
}

$indexPath = Join-Path $root 'wwwroot\dist\index.html'
$index = Get-Content -Raw -LiteralPath $indexPath
if ($index.Contains('你的正式域名')) {
    throw 'Production frontend still contains the placeholder API domain.'
}

$webFiles = @(Get-ChildItem -Recurse -File (Join-Path $root 'wwwroot'))
if ($webFiles.Count -ne 1) {
    throw "Unexpected number of published webroot files: $($webFiles.Count)"
}

Write-Host "PASS publish package: $root"
Write-Host "Frontend bytes: $((Get-Item -LiteralPath $indexPath).Length)"
