# Estacionamento — monorepo

Especificações canônicas:

- **Backend / API / DDL / TDD & CI:** [`SPEC.md`](SPEC.md) **v8.7** (§23–§26)
- **Web + Android / testes UI:** [`SPEC_FRONTEND.md`](SPEC_FRONTEND.md) **v1.4** (§13)
- **Regras para agentes de IA / humanos:** [`AGENTS.md`](AGENTS.md)

## Qualidade (TDD) e zero entrega sem testes

- [`SPEC.md` §25](SPEC.md) — hooks Git, CI obrigatória, branch protection.  
- [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — pipeline que deve estar **verde** antes de merge.  
- [`AGENTS.md`](AGENTS.md) — o que assistentes de IA **não** podem fazer (ex.: dizer “pronto” sem `dotnet test` verde).  
- [`docs/SPEC_TRACEABILITY_CHECKLIST.md`](docs/SPEC_TRACEABILITY_CHECKLIST.md) — mapa SPEC ↔ testes/código e gaps a fechar rumo a 100%.

### Após clonar (hooks)

```bash
git config core.hooksPath .githooks
```

Ou em PowerShell: `.\scripts\install-hooks.ps1`

### Verificação local (recomendado antes de `git push`)

```powershell
.\scripts\verify.ps1
```

## Estrutura de pastas esperada

Ver §1.1 em `SPEC.md` e §1.3 em `SPEC_FRONTEND.md`.

## Pré-requisitos

- .NET **10 SDK** — alinhado à `SPEC.md` §1.1 v8.7 (`net10.0`).
- Docker (Postgres local)
- Node **20** (frontend Web)
- **JDK 17** (Gradle/Android no terminal; Android Studio traz embutido)
- Android Studio **Koala+** (app)

## Execução local (sistema Web + API — uso normal)

1. **Postgres:** na raiz do repositório, `docker compose up -d` (ou `docker compose up -d --wait` se o seu Docker suportar).
2. **Variáveis:** `cp .env.example .env` e preencha `JWT_SECRET` e `PIX_WEBHOOK_SECRET` (≥ 32 caracteres cada). Para ter **super admin** de desenvolvimento (`super@test.com` / `Super!12345`), defina **`E2E_SEED=1`** no `.env` (só ambiente local).
3. **API:** na raiz, `.\scripts\run-api-local.ps1` **ou** exporte manualmente as variáveis do `.env` e execute `dotnet run` em `backend/src/Parking.Api`. Saúde: `http://localhost:8080/health`. Prefixo REST: `http://localhost:8080/api/v1`. Jobs em background: expiração PIX (`PIX_EXPIRY_JOB_SECONDS`), retenção de dados (`DATA_RETENTION_JOB_SECONDS` — idempotency 24h, webhook 30d, audit 365d). Leitura de auditoria global: `GET /api/v1/admin/audit-events` (**só SUPER_ADMIN**, query opcional `parking_id`, `limit`).
4. **Web:** `cd frontend-web`, `npm ci`, `npm run dev` → abrir **`http://localhost:5173`**. O Vite usa `VITE_API_BASE` (ex.: `http://localhost:8080/api/v1` em `.env.development`).
5. **Primeiro tenant:** com `E2E_SEED=1`, faça login como super no app ou via `POST /api/v1/auth/login`, depois `POST /api/v1/admin/tenants` (corpo com `adminEmail`, `adminPassword`, `parkingId` opcional) para criar o estacionamento e o admin do tenant.

**Testes automatizados (recomendado antes de considerar “pronto”):**

- Backend: `dotnet test backend/Parking.sln -c Release`
- Frontend (unit + cobertura): `cd frontend-web && npm ci && npm test && npm run build`
- E2E (Postgres + API com `E2E_SEED=1`): na raiz, `.\scripts\e2e-web.ps1` (Windows) ou o job equivalente no CI.

**Android:** com JDK 17, `cd android && ./gradlew test` (unitários JVM). O **CI** (`.github/workflows/ci.yml`) executa também **`connectedDebugAndroidTest`** num emulador API 30 (Ubuntu). Localmente: emulador/dispositivo + `./gradlew connectedDebugAndroidTest` para espelhar o job `android-instrumented`.

**E2E Android “completo” (SPEC_FRONTEND §13):** o repositório cobre **Compose UI Test** em login (`LoginScreenTest.kt`) e o pipeline **instrumentado** no emulador. Um **suite E2E completo** (ex.: Firebase Test Lab, fluxos ponta a ponta em dispositivo) é **opcional** na v1; ao adicionar, documente o escopo e atualize este parágrafo.

## Se algo “travar”

- **`dotnet test` lento (~15–40 s):** o projeto usa **Testcontainers** (sobe Postgres em Docker por teste).
- **E2E Playwright:** exige Postgres (`docker compose up -d`), API com **`E2E_SEED=1`** e, na primeira vez, `npx playwright install` em `frontend-web/`.
- **`gradlew` / Android:** defina **`JAVA_HOME`** apontando para o JDK 17 (ou use *Gradle JDK* no Android Studio).

## Banco de dados (local)

```bash
docker compose up -d
```

Na primeira execução, `database/init/00_create_databases.sql` cria `parking_identity` e `parking_audit`.

## Variáveis de ambiente

```bash
cp .env.example .env
# Editar JWT_SECRET, PIX_WEBHOOK_SECRET e demais valores.
```

## Backend (após implementação do código em `backend/`)

```bash
cd backend/src/Parking.Api
dotnet run
```

API esperada em `http://localhost:8080/api/v1` (ajustar conforme implementação).

## Seed de pacotes (por tenant)

Após `POST /admin/tenants` e migrations do banco `parking_{uuid}`, aplicar:

```bash
psql "postgresql://parking:parking_dev@localhost:5432/parking_<UUID_SEM_HIFEN>" -f database/seed/tenant_recharge_packages.sql
```

## PIX em modo Stub (desenvolvimento)

1. `PIX_MODE=Stub` no `.env`.
2. Fluxo normal até gerar QR.
3. Confirmar pagamento chamando o webhook com HMAC (exemplo — ajustar `payment_id` e body exato):

```bash
# Body bruto exatamente (sem espaços extras se o servidor validar byte-a-byte):
BODY='{"transaction_id":"test-tx-001","payment_id":"<PAYMENT_UUID>","status":"PAID"}'
SECRET="<mesmo PIX_WEBHOOK_SECRET do .env>"
# Linux/macOS (openssl):
SIG=$(printf '%s' "$BODY" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')
curl -sS -X POST http://localhost:8080/api/v1/payments/webhook \
  -H "Content-Type: application/json" \
  -H "X-Signature: $SIG" \
  -d "$BODY"
```

## Frontend Web (`frontend-web/`)

```bash
cd frontend-web
npm ci
npm run dev
```

- `VITE_API_BASE=http://localhost:8080/api/v1` (ver `.env.development` / `.env.example`)
- Testes unitários: `npm test`
- **E2E:** com API e Postgres no ar — `npm run test:e2e` (ou use `scripts/e2e-web.ps1` no Windows).

## App Android (`android/`)

```bash
cd android
./gradlew test        # unitários (JDK 17 no PATH)
./gradlew assembleDebug
```

`BuildConfig.API_BASE` em debug: `http://10.0.2.2:8080/api/v1` (emulador).

