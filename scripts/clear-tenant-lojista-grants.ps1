# Zera todas as bonificações (lojista_grants) nos BD de tenant.
# Repõe as horas nas carteiras dos lojistas (lojista_wallets) antes de apagar.
#
# Uso:
#   .\scripts\clear-tenant-lojista-grants.ps1 -Yes
# Um tenant:
#   .\scripts\clear-tenant-lojista-grants.ps1 -Yes -Database parking_eebf5d889e4c4d5a84aaa3c6d695d691

param(
    [Parameter(Mandatory = $true)]
    [switch]$Yes,
    [string]$Database = '',
    [string]$Container = 'parking-postgres',
    [string]$DbUser = 'parking'
)

$ErrorActionPreference = 'Stop'
$sqlFile = Join-Path $PSScriptRoot 'clear-tenant-lojista-grants.sql'
if (-not (Test-Path $sqlFile)) { Write-Error "Falta $sqlFile" }

function Get-TenantDatabases {
    $q = "SELECT datname FROM pg_database WHERE datname LIKE 'parking_%' AND datname NOT IN ('parking_identity','parking_audit') ORDER BY 1;"
    $raw = docker exec $Container psql -U $DbUser -d postgres -t -A -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { throw "docker exec falhou: $raw" }
    $raw | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
}

if (-not $Yes) {
    Write-Error 'Confirme com -Yes.'
}

$dbs = if ($Database -ne '') { @($Database) } else { @(Get-TenantDatabases) }
if ($dbs.Count -eq 0) {
    Write-Warning 'Nenhuma base parking_* encontrada.'
    exit 0
}

Write-Host "Bases: $($dbs -join ', ')"
foreach ($db in $dbs) {
    Write-Host ">>> $db"
    Get-Content -Raw $sqlFile | docker exec -i $Container psql -U $DbUser -d $db -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "Falha em $db" }
}

Write-Host 'Concluído.'
