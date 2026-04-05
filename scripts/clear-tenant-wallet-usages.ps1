# Apaga todo o histórico de consumos (wallet_usages) nos BD de tenant.
# Restaura balance_hours na carteira comprada do cliente (source = client) antes do DELETE.
#
# Uso (local, docker-compose postgres):
#   .\scripts\clear-tenant-wallet-usages.ps1 -Yes
# Apenas um tenant:
#   .\scripts\clear-tenant-wallet-usages.ps1 -Yes -Database parking_eebf5d889e4c4d5a84aaa3c6d695d691
#
# Requer: contentor parking-postgres a correr. BACKUP antes em produção.

param(
    [Parameter(Mandatory = $true)]
    [switch]$Yes,
    [string]$Database = '',
    [string]$Container = 'parking-postgres',
    [string]$DbUser = 'parking'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sqlFile = Join-Path $PSScriptRoot 'clear-tenant-wallet-usages.sql'
if (-not (Test-Path $sqlFile)) { Write-Error "Falta $sqlFile" }

function Get-TenantDatabases {
    $q = "SELECT datname FROM pg_database WHERE datname LIKE 'parking_%' AND datname NOT IN ('parking_identity','parking_audit') ORDER BY 1;"
    $raw = docker exec $Container psql -U $DbUser -d postgres -t -A -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { throw "docker exec falhou: $raw" }
    $raw | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
}

if (-not $Yes) {
    Write-Error 'Confirme com -Yes (operação destrutiva).'
}

$dbs = if ($Database -ne '') { @($Database) } else { @(Get-TenantDatabases) }
if ($dbs.Count -eq 0) {
    Write-Warning 'Nenhuma base parking_* encontrada.'
    exit 0
}

Write-Host "Bases a limpar: $($dbs -join ', ')"
foreach ($db in $dbs) {
    Write-Host ">>> $db"
    Get-Content -Raw $sqlFile | docker exec -i $Container psql -U $DbUser -d $db -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "Falha em $db" }
}

Write-Host 'Concluído.'
