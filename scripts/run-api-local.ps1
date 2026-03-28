# Sobe a API em http://localhost:8080 com variáveis do .env na raiz do repo.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) {
    Write-Error "Crie o ficheiro .env na raiz (copie de .env.example)."
}
Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -eq '' -or $line.StartsWith('#')) { return }
    $i = $line.IndexOf('=')
    if ($i -lt 1) { return }
    $name = $line.Substring(0, $i).Trim()
    $val = $line.Substring($i + 1).Trim()
    [Environment]::SetEnvironmentVariable($name, $val, 'Process')
}
if (-not $env:ASPNETCORE_URLS) { $env:ASPNETCORE_URLS = 'http://0.0.0.0:8080' }
Set-Location "$root\backend\src\Parking.Api"
# Sem perfil de lançamento: URLs vêm do .env (ex.: ASPNETCORE_URLS=http://0.0.0.0:8080)
dotnet run --no-launch-profile
