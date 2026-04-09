<#
.SYNOPSIS
  Cria um App Registration + Service Principal com permissão Contributor no resource group e mostra o JSON para colar em AZURE_CREDENTIALS no GitHub.

.PARAMETER ResourceGroup
  Resource group onde está o Container App e o ACR.

.EXAMPLE
  .\scripts\azure\create-sp-for-github.ps1 -ResourceGroup rg-parking-prod
#>
param(
  [Parameter(Mandatory = $true)][string] $ResourceGroup
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
  Write-Error 'Instale o Azure CLI.'
}

$sub = az account show --query id -o tsv
if (-not $sub) { az login; $sub = az account show --query id -o tsv }

$rgId = az group show -n $ResourceGroup --query id -o tsv
if (-not $rgId) {
  Write-Error "Resource group '$ResourceGroup' não encontrado."
}

$name = "github-parking-deploy-$(Get-Random -Maximum 99999)"

Write-Host ''
Write-Host '--- Copie o JSON abaixo para o secret AZURE_CREDENTIALS no GitHub (Settings > Secrets and variables) ---' -ForegroundColor Cyan
Write-Host ''

# --json-auth: JSON em stdout; avisos em stderr. Redirecionar para ficheiros via cmd.exe
# evita o PowerShell misturar fluxos ou partir linhas (causa JSON invalido tipo so '{').
$outPath = Join-Path $env:TEMP ('az-sp-stdout-{0}.json' -f [guid]::NewGuid().ToString('N'))
$errPath = Join-Path $env:TEMP ('az-sp-stderr-{0}.txt' -f [guid]::NewGuid().ToString('N'))
Remove-Item $outPath, $errPath -Force -ErrorAction SilentlyContinue

$safeName = $name.Replace('"', '')
$safeScope = $rgId.Replace('"', '')
# cmd: um unico comando com redirecionamento (caminhos entre aspas por causa de espacos em %TEMP%)
$cmdLine = "az ad sp create-for-rbac --name `"$safeName`" --role contributor --scopes `"$safeScope`" --json-auth 1> `"$outPath`" 2> `"$errPath`""
$p = Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', $cmdLine -Wait -PassThru -NoNewWindow
$exitCode = $p.ExitCode

$stderr = if (Test-Path $errPath) { [System.IO.File]::ReadAllText($errPath) } else { '' }
$stdout = if (Test-Path $outPath) { [System.IO.File]::ReadAllText($outPath) } else { '' }

try {
  Remove-Item $outPath, $errPath -Force -ErrorAction SilentlyContinue
}
catch { }

if ($exitCode -ne 0) {
  Write-Error @"
create-for-rbac falhou (exit $exitCode).
stderr:
$stderr
stdout:
$stdout
"@
}

$json = $stdout.Trim()
if (-not $json.StartsWith('{')) {
  Write-Error @"
Stdout nao comeca com JSON. stderr:
$stderr
stdout (inicio):
$($stdout.Substring(0, [Math]::Min(800, $stdout.Length)))
"@
}

try {
  $cred = $json | ConvertFrom-Json
  if (-not $cred.clientId -or -not $cred.clientSecret) {
    Write-Error "JSON sem clientId/clientSecret. Trecho: $($json.Substring(0, [Math]::Min(400, $json.Length)))..."
  }
}
catch {
  Write-Error "JSON invalido no stdout: $_`nTrecho: $($json.Substring(0, [Math]::Min(300, $json.Length)))..."
}

$outFile = Join-Path $env:TEMP 'azure-AZURE_CREDENTIALS-for-github.json'
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outFile, $json, $utf8NoBom)

Write-Host ''
Write-Host "JSON completo gravado em:" -ForegroundColor Green
Write-Host "  $outFile"
Write-Host ''
Write-Host '--- Cole no GitHub (secret AZURE_CREDENTIALS) ---' -ForegroundColor Cyan
Write-Host ''
# Write-Output evita alguns bugs de buffer do Write-Host com linhas muito longas
Write-Output $json
Write-Host ''
Write-Host '--- fim ---' -ForegroundColor Cyan
Write-Host ''
Write-Host 'O clientSecret nao volta a aparecer; guarde o ficheiro ou o secret em local seguro.' -ForegroundColor Yellow
