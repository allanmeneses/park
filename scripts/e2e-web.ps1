<#
.SYNOPSIS
  Playwright E2E com Postgres (Docker) + API — mesmo ambiente do CI (workflow ci.yml → job frontend-e2e).

.USAGE
  .\scripts\e2e-web.ps1                 # fluxo completo
  .\scripts\e2e-web.ps1 -Quick          # pula npm ci e playwright install (repetir testes)
  .\scripts\e2e-web.ps1 -Down           # só para: docker compose down
  .\scripts\e2e-web.ps1 -SkipDocker     # Postgres já está no ar

Variáveis compartilhadas com o CI: scripts\e2e-ci-env.ps1
#>
[CmdletBinding()]
param(
    [switch] $Down,
    [switch] $Quick,
    [switch] $SkipDocker
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $PSScriptRoot 'e2e-ci-env.ps1'

if (-not (Test-Path $envFile)) {
    throw "Arquivo não encontrado: $envFile"
}
$cfg = & $envFile

Set-Location $root

if ($Down) {
    Write-Host 'Encerrando Postgres (docker compose down)...'
    docker compose down
    exit 0
}

function Apply-HashtableToEnv {
    param([hashtable] $Map)
    foreach ($key in $Map.Keys) {
        Set-Item -Path "env:$key" -Value $Map[$key]
    }
}

if (-not $SkipDocker) {
    Write-Host 'Postgres (docker compose up -d --wait)...'
    docker compose up -d --wait
}
else {
    Write-Host 'SkipDocker: assumindo Postgres em 127.0.0.1:5432.'
}

Apply-HashtableToEnv $cfg.Api

Write-Host 'dotnet build (API Release)...'
dotnet build "$root\backend\src\Parking.Api\Parking.Api.csproj" -c Release | Out-Host

$apiLog = Join-Path $env:TEMP 'parking-api-e2e.log'
$apiErr = Join-Path $env:TEMP 'parking-api-e2e.err'
if (Test-Path $apiLog) { Remove-Item $apiLog -Force -ErrorAction SilentlyContinue }
if (Test-Path $apiErr) { Remove-Item $apiErr -Force -ErrorAction SilentlyContinue }

Write-Host 'API em http://127.0.0.1:8080 (background)...'
$api = Start-Process -FilePath 'dotnet' -ArgumentList @(
    'run',
    '--no-launch-profile',
    '--project', "$root\backend\src\Parking.Api\Parking.Api.csproj",
    '--no-build', '-c', 'Release'
) -PassThru -WindowStyle Hidden -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr

try {
    $ok = $false
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health' -UseBasicParsing -TimeoutSec 2
            if ($r.StatusCode -eq 200) { $ok = $true; break }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    if (-not $ok) {
        Write-Host '--- API stdout ---'
        Get-Content $apiLog -ErrorAction SilentlyContinue
        Write-Host '--- API stderr ---'
        Get-Content $apiErr -ErrorAction SilentlyContinue
        throw "API não respondeu em /health. Veja $apiLog e $apiErr"
    }

    Set-Location "$root\frontend-web"
    Apply-HashtableToEnv $cfg.Playwright

    if (-not $Quick) {
        npm ci
        npx playwright install chromium
    }
    else {
        if (-not (Test-Path 'node_modules')) {
            Write-Host 'Quick: node_modules ausente — executando npm ci.'
            npm ci
        }
        Write-Host 'Quick: sem npm ci nem playwright install (use sem -Quick se der erro de browser).'
    }

    npm run test:e2e
}
finally {
    if ($api -and -not $api.HasExited) {
        Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host 'Processo da API encerrado.'
}
