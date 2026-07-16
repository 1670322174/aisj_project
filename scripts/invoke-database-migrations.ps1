param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ConnectionString = $env:ConnectionStrings__DesignDB,
    [switch]$BaselineExisting
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw 'Connection string is required. Set ConnectionStrings__DesignDB or pass -ConnectionString.'
}

$mysql = Get-Command mysql -ErrorAction SilentlyContinue
if (-not $mysql) {
    throw 'mysql client was not found in PATH.'
}

$parts = @{}
foreach ($segment in $ConnectionString.Split(';')) {
    if ($segment -notlike '*=*') { continue }
    $pair = $segment.Split('=', 2)
    $parts[$pair[0].Trim().ToLowerInvariant()] = $pair[1]
}

$server = $parts['server']
$port = if ($parts['port']) { $parts['port'] } else { '3306' }
$database = $parts['database']
$user = if ($parts['uid']) { $parts['uid'] } else { $parts['user id'] }
$password = if ($parts['pwd']) { $parts['pwd'] } else { $parts['password'] }

foreach ($value in @($server, $port, $database, $user)) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -notmatch '^[A-Za-z0-9_.:-]+$') {
        throw 'Connection string contains a missing or unsupported server, port, database, or user value.'
    }
}

$mysqlArgs = @(
    "--host=$server",
    "--port=$port",
    "--user=$user",
    "--database=$database",
    '--batch',
    '--raw',
    '--default-character-set=utf8mb4',
    '--skip-column-names'
)

$previousPassword = $env:MYSQL_PWD
$env:MYSQL_PWD = $password

function Invoke-MySqlQuery([string]$Sql) {
    $output = & $mysql.Source @mysqlArgs "--execute=$Sql" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "MySQL command failed: $($output -join [Environment]::NewLine)"
    }
    return @($output)
}

function Invoke-MySqlFile([string]$Path) {
    $normalizedPath = (Resolve-Path -LiteralPath $Path).Path.Replace('\', '/')
    $output = & $mysql.Source @mysqlArgs "--execute=source $normalizedPath" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Migration failed: $Path`n$($output -join [Environment]::NewLine)"
    }
}

try {
    $databasePath = Join-Path $ProjectRoot 'InteriorDesignWeb\database'
    $ledgerPath = Join-Path $databasePath '20260712_schema_migrations.sql'
    $manifestPath = Join-Path $databasePath 'migration-order.json'

    Invoke-MySqlFile $ledgerPath

    $applied = @{}
    foreach ($line in Invoke-MySqlQuery 'SELECT CONCAT(MigrationName, CHAR(9), Checksum) FROM schema_migrations;') {
        $fields = "$line".Split("`t", 2)
        if ($fields.Length -eq 2) { $applied[$fields[0]] = $fields[1] }
    }

    $migrations = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    foreach ($migration in $migrations) {
        $path = Join-Path $databasePath $migration
        if (-not (Test-Path -LiteralPath $path)) {
            if ($applied.ContainsKey($migration)) {
                Write-Warning "SKIP applied migration whose source file is unavailable: $migration"
                continue
            }
            throw "Migration file is missing: $migration"
        }

        $checksum = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($applied.ContainsKey($migration)) {
            if ($applied[$migration] -ne $checksum) {
                throw "Applied migration was modified: $migration"
            }
            Write-Host "SKIP $migration"
            continue
        }

        $mode = if ($BaselineExisting) { 'baseline' } else { 'executed' }
        if (-not $BaselineExisting) {
            Write-Host "APPLY $migration"
            Invoke-MySqlFile $path
        } else {
            Write-Host "BASELINE $migration"
        }

        Invoke-MySqlQuery "INSERT INTO schema_migrations(MigrationName, Checksum, AppliedAt, ApplyMode) VALUES ('$migration', '$checksum', UTC_TIMESTAMP(), '$mode');" | Out-Null
    }
}
finally {
    if ($null -eq $previousPassword) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    } else {
        $env:MYSQL_PWD = $previousPassword
    }
}
