/* Static Web App (Free) — URL tipo https://<nome>.azurestaticapps.net
   Depois: GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN (Portal → SWA → Manage deployment token)
   ou: az staticwebapp secrets list -n <nome> -g <rg> */

targetScope = 'resourceGroup'

@minLength(3)
@maxLength(60)
@description('Nome globalmente único do Static Web App (define o subdomínio azurestaticapps.net).')
param staticWebAppName string

// Microsoft.Web/staticSites (Free) NÃO suporta brazilsouth. Regiões típicas: westus2, centralus, eastus2, westeurope, eastasia.
@description('Região do Static Web App (tem de ser uma suportada pelo tipo; o RG pode estar outra região).')
param location string = 'eastus2'

resource swa 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

@description('Hostname público (sem https://)')
output staticWebAppDefaultHostname string = swa.properties.defaultHostname

@description('URL pública do site')
output staticWebAppUrl string = 'https://${swa.properties.defaultHostname}'
