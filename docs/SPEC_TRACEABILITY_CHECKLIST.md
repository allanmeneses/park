# Rastreabilidade SPEC ↔ código e testes

Checklist vivo para aproximar **100%** de `SPEC.md` v8.7 e `SPEC_FRONTEND.md` v1.4.  
Última organização alinhada a **SPEC §24** (áreas de teste backend) e **§13** (DoD front).

---

## 1. Desvios normativos

| Ref. | Estado |
|------|--------|
| SPEC §1.1 | **Alinhado** — runtime .NET 10.0 normativo (v8.7); strings monetárias JSON via `MoneyFormatting` (InvariantCulture). |

---

## 2. Backend — `SPEC.md` §24 ↔ pastas de teste

| Área SPEC §24 | Pasta / classe | Ficheiros |
|---------------|----------------|-----------|
| **Infrastructure** | `E2E/Infrastructure/` | `PostgresWebAppFixture.cs`, `E2ETenantProvision.cs`, `TenantUnavailableIntegrationTests.cs` |
| **Admin / Audit / Jobs** | `E2E/Admin/` | `SuperAuditAndRetentionIntegrationTests.cs` |
| **Auth** (+ cash/settings) | `E2E/Surface/ApiSurfaceIntegrationTests.cs` | login, refresh, logout, settings 401 |
| **Tickets** + **Checkout** + **Payments** (superfície) | `E2E/Surface/` + `E2E/Tickets/` + `E2E/Flows/` | `ExtendedApiRoutesIntegrationTests` (card/cash/404/AMOUNT_MISMATCH); checkout→payment GET; `TicketsContractIntegrationTests` |
| **Webhook** | `E2E/Surface/ApiSurfaceIntegrationTests.cs` | assinatura inválida |
| **Packages** / **Dashboard** / **Checkout** avançado | `E2E/Scenarios/SpecGapIntegrationTests.cs` | CLOCK_SKEW, histórico, PIX expiry, convenio, seeds |
| **Auth** normativa §7/§17/§18 | `E2E/Auth/SpecNormativaIntegrationTests.cs` | OPERATOR_BLOCKED, unsuspend, recharge scope |
| **Payments** E2E | `E2E/Flows/E2EFlowTests.cs` | PIX + webhook fecha ticket |

### 2.1 Matriz rápida — rotas §17 / §18

| Rota / comportamento | Teste principal | Estado |
|----------------------|-----------------|--------|
| POST /auth/login, refresh, logout | `Surface` + `Auth` | ✓ |
| GET /admin/audit-events (SUPER_ADMIN) | `E2E/Admin` | ✓ |
| POST /tickets, GET open, GET {id}, checkout | `Tickets`, `Flows`, `Scenarios` | ✓ |
| GET/POST /payments/* , webhook | `Surface`, `ExtendedApiRoutes`, `Flows` | ✓ |
| GET /client/* , /lojista/* | `Scenarios`, `ExtendedApiRoutes` (wallet, buy CREDIT/PIX, GET payment) | ✓ |
| Cash, settings, dashboard, operator/problem | `Surface`, `Scenarios`, `Admin` (divergência) | ✓ |
| POST /admin/* | `Flows`, `Auth`, `ExtendedApiRoutes` (unsuspend), `Admin`, `Infrastructure` | ✓ |
| Tenant DB indisponível (503 TENANT_UNAVAILABLE) | `Infrastructure/TenantUnavailableIntegrationTests` | ✓ |

### 2.2 Backend — retenção, auditoria, alertas

| Tema SPEC | Implementação | Teste |
|-----------|---------------|--------|
| §4 Leitura audit SUPER_ADMIN | `GET /api/v1/admin/audit-events` + `SuperAuditController` | `Admin/SuperAuditAndRetentionIntegrationTests` |
| §5 jobs idempotency 24h / webhook 30d | `DataRetentionRunner` + `DataRetentionBackgroundService` | `DataRetentionRunner_remove_idempotency_*` |
| §4 audit &gt; 365 dias | `AuditRetentionRunner` (no mesmo hosted service) | `AuditRetentionRunner_remove_eventos_*` |
| §14 CASH_DIVERGENCE | `CashController.Close` + payload JSON | `Cash_close_divergencia_*` |
| §14 CONVENIO_RATIO | `DashboardController.Get` | `Scenarios/Dashboard_retorna_uso_convenio_*` (métrica); alerta INSERT no código |
| §2.1 TENANT_UNAVAILABLE 503 | `TenantResolutionMiddleware` | `TenantUnavailableIntegrationTests` |
| §23.3 cobertura | CI + `check_spec_coverage.py` | ✓ |

**Variáveis:** `DATA_RETENTION_JOB_SECONDS` (default 3600s) — ver `.env.example`.

---

## 3. Frontend Web — `SPEC_FRONTEND.md` §11, §13

### 3.1 §13.2 Vitest + cobertura

| Requisito | Onde |
|-----------|------|
| Vitest 2.x + coverage V8 | `package.json`, `vite.config.ts` |
| Linhas ≥ 60% | `vite.config.ts` → `coverage.thresholds` |
| CI `npm run test` | `ci.yml` → `frontend-web` |

### 3.2 §13.2 E2E Playwright

| # | Cenário | Ficheiro |
|---|---------|----------|
| 1–3 | Login, operador entrada, PIX+webhook | `e2e/parking.spec.ts` |

### 3.3 §13.4 — ESLint `no-console`

| Requisito | Onde |
|-----------|------|
| `npm run lint` (`eslint` + `no-console` warn) | `eslint.config.mjs`, `package.json` |
| CI | `frontend-web` job após `npm ci` |

### 3.4 §11 Acessibilidade Web

| Requisito | Estado |
|-----------|--------|
| Botões com `aria-label` | `src/views/**/*.vue` (Voltar, etc.) |
| Lista tickets operador | `OpHomeView.vue`: `role="button"`, `tabindex="0"`, Enter/Espaço, `aria-label` por linha |
| Campo placa | ✓ `OpEntryPlateView.vue` |

### 3.5 §11 Android (Compose)

| Requisito | Estado |
|-----------|--------|
| Botões e campo UUID SUPER_ADMIN | `contentDescription` alinhado a `UiStrings` (login, operador, gestor, cliente, lojista, adm, forbidden, placeholder, checkout erro) |
| TabBar Operação / Gestão | `ParkingApp.kt` — `NavigationBarItem` com `contentDescription` **B21** / **B20** |

---

## 4. Android — §13.3–13.5

| Requisito | Onde |
|-----------|------|
| `assembleDebug` + `test` | CI job `android` |
| `connectedDebugAndroidTest` | CI `android-instrumented` |
| Compose UI test | `LoginScreenTest.kt` |
| §11 por ecrã | baseline `contentDescription` nas ações principais; rever ao alterar UI |

---

## 5. CI — `.github/workflows/ci.yml`

**Estado v1 (100% checklist):** todos os jobs abaixo entendidos como obrigatórios em `main`; revalidar após alterar `.github/workflows/ci.yml` ou se algum run falhar.

- [x] `spec-present` — *Spec documents*
- [x] `backend` — *Backend (.NET)* — testes + cobertura §23.3
- [x] `frontend-web` — *Frontend Web (Vue)* — `lint`, `build`, `test`
- [x] `frontend-e2e` — *Frontend E2E (Playwright)*
- [x] `android` — *Android unit (Gradle)* — `assembleDebug test`
- [x] `android-instrumented` — *Android instrumented (SPEC_FRONTEND §13.3–13.4)*

---

## 6. Scripts locais — SPEC §26

| Script | Cobre |
|--------|--------|
| `verify.ps1` / `verify.sh` | Backend §23.3 + `npm run lint` + build + test |
| `install-hooks.ps1` | §25.3 |
| `setup-branch-protection.ps1` / `setup-branch-protection.sh` | §25.5 — `gh api` (ver `docs/BRANCH_PROTECTION.md`) |

---

## 7. Como usar

1. Antes de merge: CI verde.  
2. Nova regra: TDD + linha em §2.1 ou §3.  
3. **Escopo v1 fechado** para este repositório: SPEC ↔ testes ↔ CI ↔ scripts (hooks + branch protection). Evolução opcional: mais UI tests instrumentados ou Firebase Test Lab. Nova rota §17/§18: atualizar matriz §2.1 e testes.

Documento normativo: `SPEC.md` / `SPEC_FRONTEND.md`.
