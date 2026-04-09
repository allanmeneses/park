<#
.SYNOPSIS
  Cria o resource group (se faltar) e implanta infra/azure/main.bicep (ACR + Container Apps + placeholder).

.PARAMETER ResourceGroup
  Nome do resource group (ex.: rg-parking-prod).

.PARAMETER Location
  Região Azure (ex.: brazilsouth).

.PARAMETER AcrName
  Nome globalmente único do ACR (5–50 chars, alfanumérico).

.EXAMPLE
  .\scripts\azure\provision.ps1 -ResourceGroup rg-parking-prod -Location brazilsouth -AcrName parkingacr2026abc
#>
param(
  [Parameter(Mandatory = $true)][string] $ResourceGroup,
  [Parameter(Mandatory = $true)][string] $Location,
  [Parameter(Mandatory = $true)][string] $AcrName
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$bicep = Join-Path $root 'infra\azure\main.bicep'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
  Write-Error 'Instale o Azure CLI: https://learn.microsoft.com/cli/azure/install-azure-cli'
}

az account show *> $null
if ($LASTEXITCODE -ne 0) {
  az login
}

az group create --name $ResourceGroup --location $Location | Out-Null

$depName = 'parking-infra-{0:yyyyMMddHHmmss}' -f (Get-Date)
az deployment group create `
  --resource-group $ResourceGroup `
  --name $depName `
  --template-file $bicep `
  --parameters acrName=$AcrName | Out-Null

Write-Host "Implantação concluída ($depName). Outputs:"
az deployment group show -g $ResourceGroup -n $depName --query properties.outputs -o json

Write-Host ''
Write-Host 'Próximo: configurar GitHub Secrets (AZURE_CREDENTIALS) e Variables — ver docs/DEPLOY-AZURE.md'
