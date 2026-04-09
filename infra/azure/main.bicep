/* Infra mínima: ACR + Log Analytics + Container Apps Environment + Container App (placeholder nginx).
   O GitHub Actions substitui a imagem pela API .NET e ajusta a porta para 8080. */

targetScope = 'resourceGroup'

@minLength(5)
@maxLength(50)
@description('Nome globalmente único do Azure Container Registry (só letras e números).')
param acrName string

@description('Nome do Container App (único no resource group).')
param containerAppName string = 'parking-api'

@description('Nome do ambiente Container Apps.')
param managedEnvName string = 'parking-ca-env'

param location string = resourceGroup().location

@description('Imagem inicial até o primeiro deploy da API (público).')
param placeholderImage string = 'docker.io/library/nginx:alpine'

@description('Porta da imagem placeholder (nginx = 80).')
param placeholderTargetPort int = 80

resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-parking-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

resource cae 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: managedEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: law.properties.customerId
        sharedKey: law.listKeys().primarySharedKey
      }
    }
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: cae.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: placeholderTargetPort
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: 'workload'
          image: placeholderImage
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
      }
    }
  }
}

@description('Login server do ACR (ex.: meuregistro.azurecr.io)')
output acrLoginServer string = acr.properties.loginServer

@description('Nome do ACR')
output acrNameOut string = acr.name

@description('Nome do Container App')
output containerAppNameOut string = app.name

@description('URL pública do app (https://...)')
output containerAppUrl string = 'https://${app.properties.configuration.ingress.fqdn}'
