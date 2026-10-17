[CmdletBinding()]
param(
    [switch]$Seed5Million,
    [switch]$Local,
    [string]$DatabaseHost = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'cameras',
    [string]$Username = 'camera_demo',
    [string]$PsqlPath = 'psql'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Invoke-SqlFile([string]$Name) {
    Write-Host "Applying $Name"
    $path = Join-Path $PSScriptRoot $Name
    if ($Local) {
        # Password comes from the normal psql prompt / password file / PGPASSWORD.
        & $PsqlPath -X -h $DatabaseHost -p $Port -U $Username -d $Database -v ON_ERROR_STOP=1 -f $path
    } else {
        Get-Content -LiteralPath $path -Raw | docker compose --project-directory $projectRoot exec -T postgres `
            psql -X -U camera_demo -d cameras -v ON_ERROR_STOP=1
    }
    if ($LASTEXITCODE -ne 0) { throw "Failed: $Name (exit $LASTEXITCODE)" }
}

if (-not $Local) {
    docker compose --project-directory $projectRoot up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL did not start.' }
}

Invoke-SqlFile '001-init.sql'
Invoke-SqlFile '002-postgis.sql'
if ($Seed5Million) {
    Write-Host 'Adding 5,000,000 active cameras. This may take several minutes and several GB of disk space.'
    Invoke-SqlFile 'seed-5m.sql'
}
Write-Host 'Database is ready.'
