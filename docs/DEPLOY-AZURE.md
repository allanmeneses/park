# Deploy da API no Azure (Container Apps + ACR)

Este guia cobre só a **API .NET** em contentor. O **frontend Vue** (Vite) e a app **Android** precisam de passos extra (Static Web Apps / CDN / Play Store), que pode acrescentar depois.

## O que precisa de providenciar

1. **Conta Azure** com uma **subscrição** onde possa criar recursos (cartão / créditos).
2. **Azure CLI** instalado no seu PC: [Install Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli).
3. **GitHub** com o código neste repositório e permissão para criar **Secrets** e **Variables** no repo (ou na organização).
4. **Nome único para o ACR** (Container Registry): 5–50 caracteres, só letras e números, **globalmente único** no Azure (ex.: `parkingacr` + sufixo aleatório).
5. **PostgreSQL em produção**: a API não sobe sem bases `identity`, `audit` e template de tenant (ver `.env.example`). O mais comum é **Azure Database for PostgreSQL Flexible Server** na mesma região; tem de abrir firewall para o **outbound** do Container App (ou usar **VNet integration**). As connection strings vão para variáveis/segredos do Container App (passo 7).
6. **Segredos da aplicação**: `JWT_SECRET`, `PIX_WEBHOOK_SECRET` (e, se usar Mercado Pago, `MERCADOPAGO_*`). Gere valores fortes (≥ 32 caracteres) e guarde-os só em segredos do Azure/GitHub, nunca no código.

## Passo a passo (primeira vez)

### 1. Login e subscrição

```powershell
az login
az account set --subscription "SUBSCRIPTION_ID_OU_NOME"
```

### 2. Criar resource group e infra (ACR + Container Apps + placeholder nginx)

Escolha região (ex.: `brazilsouth`) e um nome de RG (ex.: `rg-parking-prod`).

```powershell
cd c:\PROJETOS\estacionamento
.\scripts\azure\provision.ps1 -ResourceGroup rg-parking-prod -Location brazilsouth -AcrName SEU_ACR_UNICO
```

No output, anote:

- `acrNameOut` / login server (`xxx.azurecr.io`)
- `containerAppNameOut`
- `containerAppUrl` (URL pública inicial a servir nginx até o primeiro deploy da API)

### 3. Variáveis no GitHub (não são segredos)

No repositório: **Settings → Secrets and variables → Actions → Variables**:

| Variable | Exemplo | Descrição |
|----------|---------|-----------|
| `AZURE_RESOURCE_GROUP` | `rg-parking-prod` | Resource group onde está o Container App |
| `AZURE_CONTAINER_APP_NAME` | `parking-api` | Nome do Container App (output `containerAppNameOut`) |
| `AZURE_ACR_NAME` | `seuacrunico` | Nome do ACR **sem** `.azurecr.io` |

### 4. Service Principal para o GitHub Actions fazer deploy

```powershell
.\scripts\azure\create-sp-for-github.ps1 -ResourceGroup rg-parking-prod
```

Copie o **JSON completo** que o comando imprime.

No GitHub: **Settings → Secrets and variables → Actions → New repository secret**:

- Nome: `AZURE_CREDENTIALS`
- Valor: cole o JSON (uma linha ou formatado, tanto faz).

Se o script falhar por CLI antiga, atualize o Azure CLI ou crie o SP no portal com role **Contributor** só neste resource group e monte o JSON com `clientId`, `clientSecret`, `subscriptionId`, `tenantId` no formato da [ação azure/login](https://github.com/Azure/login).

### 5. Disparar o deploy da API

Faça **merge/push para `main`** alterando `backend/`, `Dockerfile` ou o próprio workflow, ou corra manualmente **Actions → deploy-api-azure → Run workflow**.

O workflow:

1. Faz login com `AZURE_CREDENTIALS`
2. Corre `az acr build` (build na cloud, não precisa de Docker no runner)
3. Atualiza o Container App para a imagem `parking-api:<commit>` e define **ingress na porta 8080**

### 6. Validar

Abra a URL do Container App + `/health` (ex.: `https://....azurecontainerapps.io/health`). Deve responder OK quando a API estiver a correr com Postgres e variáveis corretas.

### 7. Configurar Postgres e variáveis da API (obrigatório para a app real)

Sem isto, o contentor pode falhar ao arrancar.

No portal: **Container App → Application → Containers → Environment variables**, ou na CLI:

```powershell
az containerapp update -g rg-parking-prod -n parking-api `
  --set-env-vars `
    "ASPNETCORE_ENVIRONMENT=Production" `
    "DATABASE_URL_IDENTITY=Host=...;Port=5432;Database=parking_identity;Username=...;Password=..." `
    "DATABASE_URL_AUDIT=Host=...;..." `
    "TENANT_DATABASE_URL_TEMPLATE=Host=...;Database=parking_{uuid};..." `
    "JWT_SECRET=..." `
    "PIX_WEBHOOK_SECRET=..." `
    "CORS_ORIGINS=https://seu-frontend.azurestaticapps.net"
```

Para passwords, prefira **Secrets** do Container App (`az containerapp secret set` + referência `secretref:` na documentação da Microsoft).

**Mercado Pago em produção:** defina `PAYMENT_PSP=MercadoPago` (ou `PIX_MODE=Production`), `MERCADOPAGO_ACCESS_TOKEN`, `MERCADOPAGO_WEBHOOK_SECRET`, etc., e configure o webhook para a URL pública da API (ver SPEC: path do webhook Mercado Pago).

## Ficheiros relevantes

| Ficheiro | Função |
|----------|--------|
| `Dockerfile` | Imagem da API (ASP.NET na porta 8080) |
| `infra/azure/main.bicep` | ACR, Log Analytics, ambiente Container Apps, app inicial |
| `.github/workflows/azure-api.yml` | CI deploy para ACR + atualização do Container App |
| `.github/workflows/azure-static-web.yml` | Build + deploy do `frontend-web` (Static Web Apps) |
| `scripts/azure/provision.ps1` | Implanta o Bicep no RG |
| `scripts/azure/create-sp-for-github.ps1` | Gera JSON para `AZURE_CREDENTIALS` |

## Custos e segurança

- ACR **Basic**, Container Apps com escala mínima 1 réplica e Log Analytics têm custo mensal baixo mas **não zero**.
- Rode `az ad sp create-for-rbac` só uma vez por ambiente; rotação de segredos: crie novo secret no SP, atualize `AZURE_CREDENTIALS`, apague o antigo.
- Não commite `.env` com segredos reais.

## Frontend (Azure Static Web Apps)

O repositório inclui `.github/workflows/azure-static-web.yml`, que faz **build + testes** do `frontend-web` e envia o `dist` para um **Static Web App**.

### 1. Criar o Static Web App (uma vez)

Com Azure CLI (ou use o Portal: criar recurso “Static Web App”, SKU Free).

**Região:** Static Web Apps **Free** não está disponível em **brazilsouth**. O Bicep usa por defeito **eastus2** (o resource group pode continuar em `brazilsouth`). Outras regiões suportadas: `westus2`, `centralus`, `westeurope`, `eastasia`.

```powershell
az deployment group create -g rg-parking --template-file infra/azure/static-web.bicep --parameters staticWebAppName=NOME_UNICO_GLOBAL
# opcional: --parameters staticWebAppName=... location=westeurope
```

Anote o output **`staticWebAppUrl`** (ex.: `https://NOME....azurestaticapps.net`).

### 2. Token de deploy (GitHub Secret)

No **Portal Azure** → o Static Web App → **Manage deployment token** → copiar.

No GitHub: **Settings → Secrets and variables → Actions → Secrets** → **New repository secret**:

| Name | Valor |
|------|--------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | o token copiado |

### 3. Variable `VITE_API_BASE`

**Settings → Variables → Actions** → **New repository variable**:

| Name | Valor (exemplo) |
|------|------------------|
| `VITE_API_BASE` | `https://parking-api.SEU-DOMINIO.azurecontainerapps.io/api/v1` |

Tem de ser a URL **HTTPS** da API **com** o sufixo **`/api/v1`**.

### 4. CORS na API

No Container App, defina **`CORS_ORIGINS`** a incluir o URL do site (ex.: `https://NOME.azurestaticapps.net`). Pode listar vários separados por vírgula.

### 5. Disparar o deploy

Push para `main` com alterações em `frontend-web/` ou **Actions → deploy-web-azure → Run workflow**.

### Ficheiros

| Ficheiro | Função |
|----------|--------|
| `frontend-web/public/staticwebapp.config.json` | Fallback SPA (Vue Router em history mode) |
| `infra/azure/static-web.bicep` | Recurso SWA (Free) |
| `.github/workflows/azure-static-web.yml` | Build Vite + deploy |
