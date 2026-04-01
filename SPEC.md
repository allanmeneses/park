# SPEC CANÔNICA v8.7 — SISTEMA DE ESTACIONAMENTO ENTERPRISE (FECHAMENTO)

Documento **único** de legitimidade do **backend**. Substitui v8.6 (stack §1.1 alinhada a .NET 10) e anteriores. **Frontend:** `SPEC_FRONTEND.md`.

---

## 0. Regra de execução

Implementar **exatamente** o aqui descrito. **FORA DE ESCOPO** está na §1.

---

## 1. Escopo

**Incluído:** API backend, multi-banco, regras de negócio, antifraud mínimo, offline, webhook PIX via adaptador injetável, compra de pacote (CREDIT/PIX), stub de cartão.

**FORA DE ESCOPO:** layout Android/Web, ESC/POS, adquirente real de cartão, DPO/LGPD além de L0, PCI formal.

---

## 1.1 Stack de implementação do servidor (fechada)

| Item | Valor fixo |
|------|------------|
| **Runtime** | **.NET 10.0** (SDK estável alinhado ao repositório; `TargetFramework` `net10.0`) |
| **Host** | ASP.NET Core **Web API** (minimal APIs ou controllers — **um** estilo por solução; preferir **controllers** para rotas versionadas `/api/v1`). |
| **ORM** | **Entity Framework Core 10** + **Npgsql.EntityFrameworkCore.PostgreSQL** 10.x |
| **JSON** | `System.Text.Json` (padrão ASP.NET Core) |
| **Strings monetárias na API** | Campos como `amount`, `price`, `price_per_hour`, totais de caixa: formato **`InvariantCulture`** com **`.`** decimal (ex.: `"10.50"`), independentemente da cultura do processo. |
| **Senhas** | biblioteca **Konscious.Security.Cryptography** (Argon2) ou binding para lib sodium — hash **PHC** conforme §3 |
| **JWT** | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| **HMAC webhook** | `HMACSHA256` sobre raw body |

**Estrutura de solução (monorepo na raiz do repositório `estacionamento`):**

```
estacionamento/
  backend/
    Parking.sln
    src/
      Parking.Api/              # Host, DI, middleware, Program.cs
      Parking.Application/      # Casos de uso, validações de negócio
      Parking.Domain/           # Entidades puras (opcional mínimo)
      Parking.Infrastructure/   # EF DbContexts, IPixPaymentAdapter, tenants
  frontend-web/                 # Vue — ver SPEC_FRONTEND.md
  android/                      # Android — ver SPEC_FRONTEND.md
  database/
    seed/
      tenant_recharge_packages.sql
  docker-compose.yml
  .env.example
  README.md
```

**Nomes de assembly:** `Parking.Api`, `Parking.Application`, `Parking.Infrastructure`, `Parking.Domain`.

**Testes (obrigatórios — ver §23):** projeto `Parking.Tests` (xUnit) + integração com **WebApplicationFactory** e **Testcontainers** (Postgres).

---

## 2. Arquitetura

### 2.1 Bancos

- `parking_identity` — usuários, refresh tokens.
- `parking_{uuid}` — tenant (nome físico `parking_<uuid_minúsculo_sem_hífens>`).
- `parking_audit` — append-only.

**Variáveis:** `DATABASE_URL_IDENTITY`, `DATABASE_URL_AUDIT`, `TENANT_DATABASE_URL_TEMPLATE` com `{uuid}`.

**503:** `{ "code": "TENANT_UNAVAILABLE", "message": "string" }` se o banco do tenant não conectar.

### 2.2 Tenant

- JWT: `parking_id` omitido se `SUPER_ADMIN`.
- `SUPER_ADMIN`: header **`X-Parking-Id: <uuid>`** obrigatório; ignorar `parking_id` do JWT na resolução do banco.

---

## 3. DDL — `parking_identity`

```sql
CREATE TYPE user_role AS ENUM (
  'OPERATOR','MANAGER','ADMIN','CLIENT','LOJISTA','SUPER_ADMIN'
);

CREATE TABLE users (
  id UUID PRIMARY KEY,
  email TEXT UNIQUE NOT NULL,
  password_hash TEXT NOT NULL,
  role user_role NOT NULL,
  parking_id UUID,
  entity_id UUID,
  active BOOLEAN NOT NULL DEFAULT TRUE,
  operator_suspended BOOLEAN NOT NULL DEFAULT FALSE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE TABLE refresh_tokens (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  token_hash BYTEA NOT NULL UNIQUE,
  expires_at TIMESTAMPTZ NOT NULL,
  revoked BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_refresh_user ON refresh_tokens(user_id);
```

**Integridade (backend ao criar/atualizar usuário):**

| role | parking_id | entity_id |
|------|------------|-----------|
| OPERATOR, MANAGER, ADMIN | NOT NULL | NULL |
| CLIENT | NOT NULL | = `clients.id` no tenant |
| LOJISTA | NOT NULL | = `lojistas.id` no tenant |
| SUPER_ADMIN | NULL | NULL |

**Senha:** Argon2id **m=19456, t=2, p=1**; salt 16 bytes aleatórios. **Armazenamento único:** string **PHC** (`$argon2id$v=19$...`) no campo `password_hash TEXT` — **proibido** outro formato neste projeto.

---

## 4. DDL — `parking_audit`

```sql
CREATE TABLE audit_events (
  id UUID PRIMARY KEY,
  parking_id UUID NOT NULL,
  entity_type TEXT NOT NULL,
  entity_id UUID NOT NULL,
  action TEXT NOT NULL CHECK (action IN (
    'TICKET_CREATE','CHECKOUT','PAYMENT','CASH_OPEN','CASH_CLOSE',
    'ERROR_OFFLINE','TENANT_PROVISION','PACKAGE_PURCHASE'
  )),
  payload JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE INDEX idx_audit_parking_created ON audit_events(parking_id, created_at);
```

**INSERT apenas.** Leitura: **SUPER_ADMIN**. Job diário: apagar `created_at < NOW() - interval '365 days'`.

---

## 5. DDL — `parking_{uuid}` (tenant)

```sql
CREATE TYPE ticket_status AS ENUM ('OPEN','AWAITING_PAYMENT','CLOSED');
CREATE TYPE payment_status AS ENUM ('PENDING','PAID','FAILED','EXPIRED');
CREATE TYPE payment_method AS ENUM ('PIX','CARD','CASH');
CREATE TYPE cash_session_status AS ENUM ('OPEN','CLOSED');

CREATE TABLE settings (
  id UUID PRIMARY KEY CHECK (id = '00000000-0000-0000-0000-000000000000'),
  price_per_hour NUMERIC(10,2) NOT NULL,
  capacity INT NOT NULL CHECK (capacity > 0)
);

CREATE TABLE tickets (
  id UUID PRIMARY KEY,
  plate VARCHAR(10) NOT NULL,
  entry_time TIMESTAMPTZ NOT NULL,
  exit_time TIMESTAMPTZ,
  status ticket_status NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE UNIQUE INDEX idx_ticket_active_plate
ON tickets(plate)
WHERE status IN ('OPEN','AWAITING_PAYMENT');

CREATE TABLE lojistas (
  id UUID PRIMARY KEY,
  name TEXT NOT NULL,
  hour_price NUMERIC(10,2) NOT NULL
);

CREATE TABLE lojista_wallets (
  id UUID PRIMARY KEY,
  lojista_id UUID UNIQUE NOT NULL REFERENCES lojistas(id),
  balance_hours INT NOT NULL CHECK (balance_hours >= 0)
);

CREATE TABLE clients (
  id UUID PRIMARY KEY,
  plate VARCHAR(10) UNIQUE NOT NULL,
  lojista_id UUID REFERENCES lojistas(id)
);

CREATE TABLE client_wallets (
  id UUID PRIMARY KEY,
  client_id UUID UNIQUE NOT NULL REFERENCES clients(id),
  balance_hours INT NOT NULL CHECK (balance_hours >= 0),
  expiration_date TIMESTAMPTZ
);

CREATE TABLE wallet_usages (
  id UUID PRIMARY KEY,
  ticket_id UUID NOT NULL REFERENCES tickets(id),
  source TEXT NOT NULL CHECK (source IN ('lojista','client')),
  hours_used INT NOT NULL CHECK (hours_used > 0)
);

CREATE TABLE recharge_packages (
  id UUID PRIMARY KEY,
  scope TEXT NOT NULL CHECK (scope IN ('CLIENT','LOJISTA')),
  hours INT NOT NULL CHECK (hours > 0),
  price NUMERIC(10,2) NOT NULL CHECK (price >= 0),
  active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE package_orders (
  id UUID PRIMARY KEY,
  scope TEXT NOT NULL CHECK (scope IN ('CLIENT','LOJISTA')),
  client_id UUID REFERENCES clients(id),
  lojista_id UUID REFERENCES lojistas(id),
  package_id UUID NOT NULL REFERENCES recharge_packages(id),
  status TEXT NOT NULL CHECK (status IN ('AWAITING_PAYMENT','PAID','FAILED','CANCELLED')),
  settlement TEXT NOT NULL CHECK (settlement IN ('PIX','CREDIT')),
  amount NUMERIC(10,2) NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
  paid_at TIMESTAMPTZ,
  CHECK (
    (scope = 'CLIENT' AND client_id IS NOT NULL AND lojista_id IS NULL) OR
    (scope = 'LOJISTA' AND lojista_id IS NOT NULL AND client_id IS NULL)
  )
);

CREATE TABLE payments (
  id UUID PRIMARY KEY,
  ticket_id UUID REFERENCES tickets(id),
  package_order_id UUID REFERENCES package_orders(id),
  method payment_method,
  status payment_status NOT NULL,
  amount NUMERIC(10,2) NOT NULL CHECK (amount >= 0),
  transaction_id TEXT,
  idempotency_key TEXT UNIQUE NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
  paid_at TIMESTAMPTZ,
  failed_reason TEXT,
  CHECK (
    (ticket_id IS NOT NULL AND package_order_id IS NULL) OR
    (ticket_id IS NULL AND package_order_id IS NOT NULL)
  )
);

CREATE UNIQUE INDEX uq_payment_ticket ON payments(ticket_id) WHERE ticket_id IS NOT NULL;
CREATE UNIQUE INDEX uq_payment_package_order ON payments(package_order_id) WHERE package_order_id IS NOT NULL;

CREATE TABLE pix_transactions (
  id UUID PRIMARY KEY,
  payment_id UUID NOT NULL REFERENCES payments(id) ON DELETE CASCADE,
  provider_status TEXT NOT NULL,
  qr_code TEXT NOT NULL,
  expires_at TIMESTAMPTZ NOT NULL,
  transaction_id TEXT UNIQUE,
  active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE UNIQUE INDEX uq_pix_one_active_per_payment ON pix_transactions(payment_id) WHERE active;

CREATE TABLE cash_sessions (
  id UUID PRIMARY KEY,
  status cash_session_status NOT NULL,
  opened_at TIMESTAMPTZ NOT NULL,
  closed_at TIMESTAMPTZ,
  expected_amount NUMERIC(10,2) NOT NULL DEFAULT 0,
  actual_amount NUMERIC(10,2)
);

CREATE UNIQUE INDEX uq_one_open_cash_session ON cash_sessions ((1)) WHERE status = 'OPEN';

CREATE TABLE operator_events (
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL,
  type TEXT NOT NULL CHECK (type = 'PROBLEM'),
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE INDEX idx_operator_user_day ON operator_events(user_id, created_at);

CREATE TABLE wallet_ledger (
  id UUID PRIMARY KEY,
  client_id UUID REFERENCES clients(id),
  lojista_id UUID REFERENCES lojistas(id),
  delta_hours INT NOT NULL,
  amount NUMERIC(10,2) NOT NULL,
  package_id UUID REFERENCES recharge_packages(id),
  settlement TEXT CHECK (settlement IN ('PIX','CREDIT')),
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
  CHECK (
    (client_id IS NOT NULL AND lojista_id IS NULL) OR
    (client_id IS NULL AND lojista_id IS NOT NULL)
  )
);

CREATE TABLE alerts (
  id UUID PRIMARY KEY,
  type TEXT NOT NULL CHECK (type IN ('CASH_DIVERGENCE','CONVENIO_RATIO','OTHER')),
  payload JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE TABLE schema_migrations (
  version TEXT PRIMARY KEY,
  applied_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE TABLE idempotency_store (
  key TEXT NOT NULL,
  route TEXT NOT NULL,
  response_json JSONB NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
  PRIMARY KEY (key, route)
);

CREATE TABLE webhook_receipts (
  transaction_id TEXT PRIMARY KEY,
  payment_id UUID NOT NULL,
  processed_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);

CREATE INDEX idx_webhook_receipts_processed ON webhook_receipts(processed_at);
```

**Jobs:** apagar `idempotency_store` com `created_at < NOW() - 24h`. Apagar `webhook_receipts` com `processed_at < NOW() - 30 days`.

---

## 6. Placa

Normalizar: maiúsculas, remover espaços e hífens.

- Mercosul: `^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$`
- Legado: `^[A-Z]{3}[0-9]{4}$`

Válido se **um** dos dois. Senão `400` `PLATE_INVALID`.

---

## 7. JWT e auth

- Access JWT **HS256**, claims: `iss=parking-identity`, `aud=parking-api`, `sub`=`user_id`, `role`, `parking_id` (omitir se null), `entity_id` (omitir se null), `iat`, `exp`; `exp = iat + 28800`.
- Refresh opaco; persistir **SHA-256** em `refresh_tokens.token_hash`; validade **30 dias**.
- Clock skew **±120s**.
- Login: máx. **10** falhas / **15 min** / email → `429` `LOGIN_THROTTLED`.
- `401` `OPERATOR_BLOCKED` se `operator_suspended=true` OU (`role=OPERATOR` e `PROBLEM` no dia UTC > 3).
- `POST /admin/operators/{user_id}/unsuspend` — **ADMIN** (tenant do usuário) ou **SUPER_ADMIN**; define `operator_suspended=false`.

---

## 8. Checkout — algoritmo completo (determinístico)

`POST /tickets/{id}/checkout` — header **`Idempotency-Key`** obrigatório.

**Idempotência:** mesma chave + mesmo `ticket_id` → **mesma** `response_json` 200 armazenada em `idempotency_store` (chave composta `Idempotency-Key` + rota normalizada).

**Transação:**

1. `SELECT tickets WHERE id=:id FOR UPDATE`. Se não existe → `404`. Se `status != OPEN` → `409` `INVALID_TICKET_STATE`.

2. `exit_time` = body `exit_time` (ISO8601) se presente, senão `NOW() AT TIME ZONE 'UTC'`. Se `exit_time < entry_time` → `400` `VALIDATION_ERROR`.

3. `horas_total = CEIL(GREATEST(0, EXTRACT(EPOCH FROM (exit_time - entry_time)) / 3600))::int`

4. Carregar `settings` singleton. `price = price_per_hour`.

5. **Cliente por placa:** `client = SELECT * FROM clients WHERE plate = ticket.plate` (uma linha ou zero).

6. **`horas_lojista = 0`**, **`horas_cliente = 0`**.

7. **Lojista (convênio):**  
   - Se **não existe** `client` → pular.  
   - Se `client.lojista_id IS NULL` → pular.  
   - Senão: `lw = SELECT * FROM lojista_wallets WHERE lojista_id = client.lojista_id`.  
   - Se **nenhuma linha** → `409` `LOJISTA_WALLET_MISSING`.  
   - `horas_lojista = MIN(horas_total, lw.balance_hours)`.  
   - Se `horas_lojista > 0`: `UPDATE lojista_wallets SET balance_hours = balance_hours - horas_lojista`; `INSERT wallet_usages(ticket_id, 'lojista', horas_lojista)`.

8. **Cliente (carteira horas):**  
   - `horas_restantes = horas_total - horas_lojista`.  
   - Se **não existe** `client` → `saldo_efetivo = 0`.  
   - Senão: `cw = SELECT * FROM client_wallets WHERE client_id = client.id`. Se **não existe linha** → `saldo_efetivo = 0`.  
   - Senão: se `cw.expiration_date IS NOT NULL` e `cw.expiration_date < NOW() AT TIME ZONE 'UTC'` → `saldo_efetivo = 0`; senão `saldo_efetivo = cw.balance_hours`.  
   - `horas_cliente = MIN(horas_restantes, saldo_efetivo)`.  
   - Se `horas_cliente > 0`: `UPDATE client_wallets SET balance_hours = balance_hours - horas_cliente`; `INSERT wallet_usages(ticket_id, 'client', horas_cliente)`.

9. `horas_pagaveis = horas_total - horas_lojista - horas_cliente` (garantir ≥ 0).

10. `amount = ROUND(horas_pagaveis * price, 2)` com **half up** (equivalente `ROUND(numeric, 2)` no PostgreSQL).

11. **Se `amount = 0`:**  
    - `INSERT payments(ticket_id, method NULL, status PAID, amount 0, idempotency_key, paid_at = NOW() UTC)`.  
    - `UPDATE tickets SET status=CLOSED, exit_time=exit_time`.  
    - Audit `CHECKOUT` e `PAYMENT` (payloads §15).  
    - Resposta **200** (ver §18).

12. **Se `amount > 0`:**  
    - `INSERT payments(ticket_id, method NULL, status PENDING, amount, idempotency_key)`.  
    - `UPDATE tickets SET status=AWAITING_PAYMENT, exit_time=exit_time`.  
    - Audit `CHECKOUT`.  
    - Resposta **200** (ver §18).

---

## 9. Adaptador PIX (obrigatório)

Interface lógica (implementação concreta fora de escopo do domínio, mas **contrato fixo**):

**Entrada:** `{ payment_id: uuid, amount: numeric(10,2), expires_in_seconds: int }` com `expires_in_seconds = 300` salvo config `PIX_DEFAULT_TTL_SECONDS` (default **300**).

**Saída:** `{ qr_code: string, expires_at: timestamptz, provider_transaction_id: string | null }`

- `pix_transactions.qr_code` = `qr_code`.  
- `expires_at` = saída do provedor; se ausente, `NOW() UTC + expires_in_seconds`.  
- `provider_status` = `"CREATED"` na criação.  
- `transaction_id` preenchido quando o provedor devolver; pode ser `NULL` até o webhook.

**Segredo webhook:** env `PIX_WEBHOOK_SECRET` (mesmo usado em HMAC).

### 9.1 Modos de operação (PIX)

Variável **`PIX_MODE`** (string, case-insensitive):

| Valor | Comportamento |
|-------|----------------|
| **`Stub`** (padrão em desenvolvimento) | Implementação **`StubPixProvider`**: gera `qr_code` como string **EMV simulada** (prefixo fixo `00020126...` truncada ou payload legível `PIXSTUB|{payment_id}` com comprimento mínimo 32 caracteres para o front gerar QR); `provider_transaction_id` = **novo UUID** ao criar cobrança; `expires_at` = `NOW() UTC + TTL`. **Não** chama HTTP externo. Confirmação de pagamento em ambiente de teste: cliente HTTP deve enviar **`POST /payments/webhook`** com corpo e HMAC válidos (ex.: script em `README.md`). |
| **`Production`** | Implementação concreta acoplada a **um** PSP (ex.: Efí / Gerencianet API Pix v2, Banco Inter, etc.). **Credenciais** apenas por variáveis de ambiente (`PIX_PSP_*` — prefixo definido no código do adaptador escolhido). Se credenciais ausentes na subida: **falha de host** (`IHost` não inicia) com mensagem explícita. |

**Registro DI (referência):** `services.AddSingleton<IPixPaymentAdapter, StubPixProvider>()` quando `PIX_MODE=Stub`; caso contrário `ProductionPixProvider` (nome fixo no código).

**Interface C# referência (Infrastructure):**

```csharp
public interface IPixPaymentAdapter
{
    Task<PixChargeResult> CreateChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct);
}
public sealed record PixChargeResult(string QrCode, DateTimeOffset ExpiresAt, string? ProviderTransactionId);
```

---

## 10. Pagamentos — PIX / cartão / dinheiro

**Pré-condição comum (ticket):** `ticket.status = AWAITING_PAYMENT`, existe `payment` com `ticket_id`, `status = PENDING`, `amount > 0`.

### POST /payments/pix `{ "payment_id": "uuid" }`

1. `SELECT payment FOR UPDATE`.  
2. Se `status = PAID` → `409` `PAYMENT_ALREADY_PAID`.  
3. Se `status = EXPIRED` → transição **retry:** `UPDATE payments SET status=PENDING, failed_reason=NULL` (ticket continua `AWAITING_PAYMENT`).  
4. Se `package_order_id NOT NULL` → pré-condição análoga no pedido (`package_orders.status = AWAITING_PAYMENT`).  
5. `method = PIX` (UPDATE se NULL).  
6. Se existe `pix_transactions` com `active=true` e `expires_at > NOW() UTC` → **200** com mesmo QR (resposta §18).  
7. Se existe ativo expirado: `active=false`.  
8. Chamar adaptador PIX; `INSERT pix_transactions` com `active=true`; demais `active=false` para esse `payment_id`.

### POST /payments/card `{ "payment_id", "amount" }`

Se `amount` ≠ `payment.amount` (comparação decimal exata) → `409` `AMOUNT_MISMATCH`.  
`UPDATE payments SET method=CARD, status=PAID, paid_at=NOW() UTC`.

- **Se `ticket_id` NOT NULL:** `UPDATE tickets SET status=CLOSED`. Audit `PAYMENT`.  
- **Se `package_order_id` NOT NULL:** `UPDATE package_orders SET status=PAID, paid_at=NOW() UTC`; creditar horas do pacote (criar `client_wallets`/`lojista_wallets` com saldo 0 se ausente, depois somar); `INSERT wallet_ledger`; audit `PACKAGE_PURCHASE` (mesmos efeitos do §11 item 10, **exceto** `webhook_receipts`).

### POST /payments/cash `{ "payment_id" }`

Pré: existe `cash_sessions` `OPEN` (único). Senão `409` `CASH_SESSION_REQUIRED`.

`UPDATE payments SET method=CASH, status=PAID, paid_at=NOW() UTC`; `expected_amount += payment.amount` na sessão aberta.

- **Se `ticket_id` NOT NULL:** `UPDATE tickets SET status=CLOSED`. Audit `PAYMENT`.  
- **Se `package_order_id` NOT NULL:** mesmo bloco pacote que **CARD** acima.

---

## 11. Webhook `POST /payments/webhook`

**Sem JWT.**

Header: `X-Signature` = **hexadecimal minúsculo** de **HMAC-SHA256**(`PIX_WEBHOOK_SECRET`, **raw body** bytes UTF-8).

Body JSON exato (sem espaços extras se o cliente validar byte-a-byte; **recomendação implementação:** calcular HMAC sobre os bytes recebidos antes do parse):

```json
{ "transaction_id": "string", "payment_id": "uuid", "status": "PAID" }
```

**Processamento:**

1. Validar HMAC; senão `401` `WEBHOOK_SIGNATURE_INVALID`.  
2. Se `status != "PAID"` → `400` `VALIDATION_ERROR`.  
3. Se `transaction_id` já em `webhook_receipts` → **200** `{ "ok": true, "duplicate": true }`.  
4. Carregar `payment` com `FOR UPDATE`. Se não existe → `404`.  
5. Se `payment.status = PAID` → **200** `{ "ok": true, "ignored": true }`.  
6. Se `payment.status = EXPIRED` ou `FAILED` → **409** `WEBHOOK_LATE`.  
7. Se `payment.status != PENDING` → **409** `INVALID_PAYMENT_STATE`.  
8. `UPDATE payments SET status=PAID, paid_at=NOW() UTC, method=COALESCE(method,'PIX')`.  
9. **Se `ticket_id` NOT NULL:** `UPDATE tickets SET status=CLOSED`.  
10. **Se `package_order_id` NOT NULL:** `UPDATE package_orders SET status=PAID, paid_at=NOW() UTC`; creditar `recharge_packages.hours` em `client_wallets` ou `lojista_wallets` (criar wallet com saldo 0 se não existir — **INSERT** wallet com `balance_hours=hours` se novo); `INSERT wallet_ledger`; audit `PACKAGE_PURCHASE`.  
11. `INSERT webhook_receipts(transaction_id, payment_id)`.  
12. Audit `PAYMENT`.  
13. **200** `{ "ok": true }`.

---

## 12. Expiração PIX (job)

**A cada 60 segundos** (config `PIX_EXPIRY_JOB_SECONDS=60`):

Para cada `payments` com `status=PENDING` e (`method IS NULL` OR `method='PIX'`) e existir `pix_transactions` com `active=true` e `expires_at < NOW() UTC`:

- `UPDATE pix_transactions SET active=false WHERE id IN (...)`.
- `UPDATE payments SET status=EXPIRED, failed_reason='PIX_EXPIRED' WHERE id=:pid`.

**Ticket:** permanece `AWAITING_PAYMENT`. Novo pagamento: **`POST /payments/pix`** reativa `PENDING` conforme §10.

**Pacote:** `package_orders` permanece `AWAITING_PAYMENT`; mesmo retry via `/payments/pix`.

---

## 13. Compras de pacote

**POST /client/buy** — `Idempotency-Key` obrigatório. Body `{ "package_id", "settlement": "CREDIT"|"PIX" }`.

- Validar pacote `active`, `scope=CLIENT`. `JWT.entity_id` = `clients.id` do tenant.

**CREDIT:** `package_orders` `PAID`, `paid_at=NOW()`, creditar horas, `wallet_ledger` `settlement=CREDIT`, audit. **Sem** linha em `payments`.

**PIX:** `package_orders` `AWAITING_PAYMENT`, `INSERT payments` `PENDING` com `package_order_id`, `amount=package.price`, `idempotency_key`; resposta com `payment_id`, `order_id`; cliente chama `POST /payments/pix`.

**POST /lojista/buy** — análogo, `scope=LOJISTA`, `entity_id` = lojista.

**Wallet ausente ao creditar (pacote pago):** se não existir `client_wallets`/`lojista_wallets`, **INSERT** com `balance_hours=0` antes de somar horas.

---

## 14. Antifraude e dashboard (UTC)

**Caixa ao fechar:** `divergencia = 0` se `expected=0`, senão `ABS(actual-expected)/expected`; se `> 0.05` → `INSERT alerts` `CASH_DIVERGENCE`.

**Convênio:** numerador = COUNT DISTINCT `wallet_usages.ticket_id` JOIN `payments` ON … `source='lojista'`, `payments.status='PAID'`, `(paid_at AT TIME ZONE 'UTC')::date = D`. Denominador = COUNT `tickets` `CLOSED` com `(exit_time AT TIME ZONE 'UTC')::date = D`. `D = (NOW() AT TIME ZONE 'UTC')::date`. Se denom=0, não calcular ratio; se `> 0.2` → alerta `CONVENIO_RATIO`.

**Dashboard:** ver §18 `GET /dashboard`.

---

## 15. Auditoria — payload mínimo

| action | payload obrigatório (campos) |
|--------|------------------------------|
| TICKET_CREATE | `ticket`: objeto ticket após insert |
| CHECKOUT | `ticket_id`, `exit_time`, `hours_total`, `hours_lojista`, `hours_cliente`, `amount`, `payment_id` se houver |
| PAYMENT | `payment_id`, `from_status`, `to_status` |
| PACKAGE_PURCHASE | `order_id`, `package_id`, `settlement` |
| CASH_OPEN / CASH_CLOSE | `session_id`, `expected_amount`, `actual_amount` (quando aplicável) |
| ERROR_OFFLINE | `operation`, `code`, `idempotency_key` |
| TENANT_PROVISION | `parking_id`, `admin_user_id` |

**QR e segredos:** não duplicar QR integral em audit; pode hash ou truncar.

---

## 16. Offline

Fila: `POST /tickets`, `POST /tickets/{id}/checkout` com `Idempotency-Key`. Proibido enfileirar `/payments/*`.

Se `exit_time` no body e `|device_now - server_now| > 300s` → `400` `CLOCK_SKEW`.

---

## 17. RBAC — matriz por rota

Prefixo `/api/v1`. **401** se não autenticado; **403** se autenticado sem permissão.

| Rota | OPERATOR | MANAGER | ADMIN | CLIENT | LOJISTA | SUPER_ADMIN |
|------|:--------:|:-------:|:-----:|:------:|:-------:|:-----------:|
| POST /auth/login, refresh, logout | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| POST /tickets | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /tickets/open, GET /tickets/{id} | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| POST /tickets/{id}/checkout | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /payments/{id} | ✓ | ✓ | ✓ | ✓° | ✓° | ✓* |
| POST /payments/pix,card,cash | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /recharge-packages | ✗ | ✓ | ✓ | ✓°° | ✓°° | ✓* |
| GET /client/wallet, history, POST /client/buy | ✗ | ✗ | ✗ | ✓ | ✗ | ✗ |
| GET /lojista/wallet, history, POST /lojista/buy | ✗ | ✗ | ✗ | ✗ | ✓ | ✗ |
| POST /cash/open, /cash/close, GET /cash | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /settings, POST /settings | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /dashboard | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /manager/movements | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| GET /manager/analytics | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| POST /operator/problem | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| POST /admin/operators/{id}/unsuspend | ✗ | ✗ | ✓ | ✗ | ✗ | ✓ |
| POST /admin/tenants | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ |

\*Requer `X-Parking-Id`.  
**°** `GET /payments/{id}`: **CLIENT** apenas se o pagamento tiver `package_order_id` e o pedido for desse cliente (`package_orders.client_id = JWT.entity_id`). **LOJISTA** analogamente com `lojista_id`. Caso contrário **403** `FORBIDDEN`.  
**°°** `GET /recharge-packages`: **CLIENT** só pode `scope=CLIENT`. **LOJISTA** só `scope=LOJISTA`. **MANAGER/ADMIN/SUPER_ADMIN** podem qualquer `scope`. Violação → **403**.

**POST /payments/webhook:** **não** usa JWT. Autenticação **somente** `X-Signature` (HMAC). Nenhuma coluna RBAC aplica-se; allowlist de IP é **FORA DE ESCOPO**.

---

## 18. Contratos HTTP completos

**Erro padrão:** `{ "code": "<CODE>", "message": "<string>" }`.

**Códigos fechados:**  
`VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `PLATE_INVALID`, `PLATE_HAS_ACTIVE_TICKET`, `INVALID_TICKET_STATE`, `LOJISTA_WALLET_MISSING`, `PAYMENT_ALREADY_PAID`, `AMOUNT_MISMATCH`, `CASH_SESSION_REQUIRED`, `OPERATOR_BLOCKED`, `TENANT_UNAVAILABLE`, `LOGIN_THROTTLED`, `WEBHOOK_SIGNATURE_INVALID`, `WEBHOOK_LATE`, `INVALID_PAYMENT_STATE`, `CLOCK_SKEW`, `INTERNAL`.

### POST /auth/login

Request: `{ "email": "a@b.com", "password": "..." }`  
Response **200:** `{ "access_token": "jwt", "refresh_token": "opaco", "expires_in": 28800 }`

### POST /auth/refresh

Request: `{ "refresh_token": "..." }`  
Response **200:** igual login (novo par).

### POST /auth/logout

Request: `{ "refresh_token": "..." }`  
Response **200:** `{ "ok": true }`

### POST /tickets

Header: `Idempotency-Key`.  
Request: `{ "plate": "ABC1D23" }`  
Response **201:** `{ "id": "uuid", "plate": "...", "status": "OPEN", "entry_time": "ISO8601" }`

### GET /tickets/open

Response **200:** `{ "items": [ { "id", "plate", "entry_time", "status" } ] }`

### GET /tickets/{id}

Response **200:**

```json
{
  "ticket": {
    "id": "uuid",
    "plate": "ABC1D23",
    "entry_time": "ISO8601",
    "exit_time": null,
    "status": "OPEN|AWAITING_PAYMENT|CLOSED",
    "created_at": "ISO8601"
  },
  "payment": <PaymentDTO> | null
}
```

`PaymentDTO` **idêntico** ao de `GET /payments/{id}` quando existir pagamento para o ticket; senão `null`.

### GET /payments/{id}

**Leitura** para polling de PIX e conferência de estado. Regras **°** na matriz RBAC.

Response **200:**

```json
{
  "id": "uuid",
  "status": "PENDING|PAID|FAILED|EXPIRED",
  "method": "PIX|CARD|CASH|null",
  "amount": "0.00",
  "ticket_id": null,
  "package_order_id": null,
  "paid_at": null,
  "created_at": "ISO8601",
  "failed_reason": null,
  "pix": null
}
```

`pix`: se existir `pix_transactions` com `active=true` para este pagamento, então  
`{ "expires_at": "ISO8601", "active": true }`; caso contrário `null`.

### GET /settings

Roles: **MANAGER**, **ADMIN**, **SUPER_ADMIN**\*.

Response **200:** `{ "price_per_hour": "5.00", "capacity": 50 }` (valores exemplares; refletem o tenant).

### GET /recharge-packages

Query **obrigatória:** `scope=CLIENT` ou `scope=LOJISTA` (regras **°°**).

Response **200:** `{ "items": [ { "id", "scope", "hours", "price" } ] }` — somente pacotes `active=true`.

### POST /tickets/{id}/checkout

Header: `Idempotency-Key`.  
Request: `{ "exit_time": "ISO8601 opcional" }`  
Response **200:**

```json
{
  "ticket_id": "uuid",
  "hours_total": 0,
  "hours_lojista": 0,
  "hours_cliente": 0,
  "hours_paid": 0,
  "amount": "0.00",
  "payment_id": "uuid"
}
```

`hours_paid` = `horas_pagaveis`. `payment_id` = id da linha `payments` criada (**sempre** presente: `PENDING` se `amount>0`, ou `PAID` se `amount=0`).

### POST /payments/pix | card | cash

**pix** Response **200:** `{ "payment_id", "qr_code", "expires_at": "ISO8601" }`  
**card** Response **200:** `{ "payment_id", "status": "PAID" }`  
**cash** Response **200:** `{ "payment_id", "status": "PAID" }`

### GET /client/wallet

Response **200:** `{ "balance_hours": 0, "expiration_date": null | "ISO8601" }`

### GET /client/history

Query: `limit` default 50 máx 100, `cursor` opcional **opaque** (base64url de `created_at|id`).  
Response **200:** `{ "items": [ { "id", "kind": "PURCHASE|USAGE", "delta_hours", "amount", "created_at", "ref": { "type": "ticket|package", "id": "uuid" } } ], "next_cursor": null | "string" }`

**USAGE:** de `wallet_usages` join `tickets` onde `clients.plate = tickets.plate` do JWT client.  
**PURCHASE:** de `wallet_ledger` para o `client_id`.

### GET /lojista/wallet | history

Análogo; USAGE pode ser vazio se não houver por ticket.

### POST /client/buy | POST /lojista/buy

Response **200 CREDIT:** `{ "order_id", "status": "PAID", "balance_hours": n }`  
Response **200 PIX:** `{ "order_id", "payment_id", "status": "AWAITING_PAYMENT" }`

### POST /cash/open

Response **200:** `{ "session_id": "uuid", "opened_at": "ISO8601" }`

### POST /cash/close

Request: `{ "session_id": "uuid", "actual_amount": "123.45" }`  
Response **200:** `{ "session_id", "expected_amount", "actual_amount", "divergence": 0.0, "alert": false }`

### GET /cash

Response **200:** `{ "open": { "session_id", "opened_at", "expected_amount" } | null, "last_closed": { ... } | null }`

### POST /settings

Request: `{ "price_per_hour": "10.00", "capacity": 100 }`  
Response **200:** `{ "ok": true }`

### POST /operator/problem

Request: `{}`  
Response **200:** `{ "ok": true }`

### POST /admin/operators/{user_id}/unsuspend

Response **200:** `{ "ok": true }`

### POST /admin/tenants

**Somente SUPER_ADMIN.** O papel **ADMIN** (administrador do tenant) **não** pode criar estacionamento novo; acede apenas ao `parking_id` do seu utilizador. Request (JSON camelCase típico da API):

```json
{
  "parkingId": "uuid opcional — se omitido servidor gera",
  "adminEmail": "admin@estacionamento.com",
  "adminPassword": "SenhaForte123!",
  "operatorEmail": "operador@estacionamento.com",
  "operatorPassword": "OutraSenha123!"
}
```

Regras: `operatorEmail` e `operatorPassword` obrigatórios; `adminEmail` e `operatorEmail` devem ser **distintos** (normalização case-insensitive).  
Erros: `400` `VALIDATION_ERROR` (campos em falta ou e-mails iguais); `409` `CONFLICT` se qualquer e-mail já existir em **identity**.

Efeitos:

1. Se `parkingId` omitido, gerar UUID v4.  
2. `CREATE DATABASE parking_<uuid_sem_hifen>`.  
3. Rodar todas as migrations do tenant na ordem `schema_migrations`.  
4. `INSERT settings` singleton `price_per_hour=5.00`, `capacity=50` (defaults fixos).  
5. `INSERT` em **identity** (transação): utilizador **ADMIN** do tenant e utilizador **OPERATOR** do mesmo `parking_id`, ambos com `password_hash` Argon2id e `active=true`.  
6. Opcional: `INSERT parking_audit` com `action=TENANT_PROVISION`.

Response **201:**

```json
{
  "parkingId": "uuid",
  "databaseName": "parking_<...>",
  "adminUserId": "uuid",
  "operatorUserId": "uuid"
}
```

### GET /dashboard

Response **200:**

```json
{
  "faturamento": 0.0,
  "ocupacao": 0.0,
  "tickets_dia": 0,
  "uso_convenio": null
}
```

Definições (`D` = `(NOW() AT TIME ZONE 'UTC')::date`):

- `faturamento` = `SUM(amount)` de `payments` `PAID` com `(paid_at AT TIME ZONE 'UTC')::date = D`.  
- `ocupacao` = `COUNT(*) FILTER (WHERE status='OPEN')::numeric / capacity` (capacity de `settings`).  
- `tickets_dia` = COUNT `tickets` com `(exit_time AT TIME ZONE 'UTC')::date = D` e `exit_time IS NOT NULL`.  
- `uso_convenio` = numerador/denominador §14 ou `null` se denom=0.

### GET /health

**200:** `{ "ok": true }`

---

## 19. Docker e ambiente local

**Arquivo normativo na raiz:** `docker-compose.yml` — **PostgreSQL 16** exposto em **`localhost:5432`**, usuário/senha **`parking` / `parking_dev`**, volume nomeado para dados.

A API **não** precisa estar no Compose na v1; desenvolvedor roda `dotnet run` em `backend/src/Parking.Api` apontando para o Postgres do Compose.

**Variáveis:** ver **`.env.example`** na raiz (copiar para `.env` e ajustar). Lista mínima:

- `DATABASE_URL_IDENTITY` — Npgsql connection string para banco `parking_identity`  
- `DATABASE_URL_AUDIT` — idem `parking_audit`  
- `TENANT_DATABASE_URL_TEMPLATE` — `Host=localhost;Port=5432;Username=parking;Password=parking_dev;Database=parking_{uuid};` (formato exato pode usar **Npgsql** key-value; `{uuid}` **sem hífens**)  
- `JWT_SECRET` — mínimo 32 caracteres aleatórios  
- `PIX_WEBHOOK_SECRET` — mínimo 32 caracteres  
- `PIX_MODE` — `Stub` ou `Production`  
- `CORS_ORIGINS` — ex.: `http://localhost:5173`  
- `ASPNETCORE_URLS` — `http://0.0.0.0:8080`

**Criação dos bancos globais:** script SQL único **`database/init/00_create_databases.sql`** (criar `parking_identity`, `parking_audit` vazios) executado **uma vez** contra o Postgres do Compose antes da primeira migração EF.

---

## 20. Máquinas de estado (resumo)

- Ticket: `OPEN` → `AWAITING_PAYMENT` → `CLOSED`; exceção checkout `amount=0` → direto `CLOSED`.  
- Payment: `PENDING` → `PAID` | `FAILED` | `EXPIRED`; retry `EXPIRED` → `PENDING` ao chamar `/payments/pix`.  
- `package_order` PIX: `AWAITING_PAYMENT` → `PAID` | `FAILED` | `CANCELLED`.  
- Cash: um `OPEN` por tenant.

---

## 21. Migrações e seed

1. **EF Core 10:** três contextos ou **um** contexto identity + audit + factory para tenant — **obrigatório** aplicar migrations na ordem numérica em `schema_migrations`.  
2. **Após** existir pelo menos **um** tenant (`POST /admin/tenants`), executar no banco **`parking_{uuid}`** o arquivo **`database/seed/tenant_recharge_packages.sql`** (pacotes de exemplo CLIENT/LOJISTA). Pode ser automatizado no provisionamento (passo extra após migrations do tenant).  
3. **Operador inicial:** o `POST /admin/tenants` cria também o **primeiro operador** do tenant (e-mail e senha no corpo).

---

## 22. Identificadores Android (referência de repositório)

- **`applicationId`:** `com.estacionamento.parking`  
- **Namespace Kotlin:** `com.estacionamento.parking`

*(Detalhe em `SPEC_FRONTEND.md` §1.3.)*

---

## 23. Qualidade, TDD e definição de pronto

Esta secção é **normativa**. O objetivo é que o sistema **só seja considerado utilizável** quando a automação demonstrar o comportamento esperado — **sem depender de teste manual** do product owner para validar cada entrega (aceite substituído por **suíte verde + DoD**).

### 23.1 TDD (ciclo obrigatório por funcionalidade)

Para cada **nova** regra de negócio ou **novo** endpoint:

1. Escrever **teste automatizado** que **falhe** (red) — nível adequado: unitário (domínio) ou integração (API).  
2. Implementar o **mínimo** para o teste passar (green).  
3. Refatorar mantendo os testes verdes (refactor).

**Exceção controlada:** correção de bug de produção pode começar por teste de regressão que reproduz o bug, depois fix — equivalente TDD.

### 23.2 Pirâmide de testes — backend

| Camada | Escopo | Ferramentas de referência (.NET 10) |
|--------|--------|-------------------------------------|
| **Unit** | Regras puras (cálculo de horas, arredondamento, validações sem I/O) | xUnit, asserções em `Parking.Application` |
| **Integração** | HTTP real → API → Postgres real (**container**) | `WebApplicationFactory`, **Testcontainers.PostgreSQL**, `HttpClient` |
| **Contrato** | Status HTTP, shape JSON dos erros `{ code, message }`, headers `Authorization` / `Idempotency-Key` | Mesma base de integração; opcional snapshot de JSON |
| **E2E API** (fluxo mínimo) | Cadeia completa em ambiente **dev** com `PIX_MODE=Stub` | Um teste (ou poucos) que: provisiona tenant (ou usa fixture), cria usuário operador, **login**, **POST /tickets**, **checkout**, **POST /payments/pix**, **POST /payments/webhook** com HMAC válido, assert ticket `CLOSED` |

**Proibido** substituir integração de API por apenas mocks do DbContext para **endpoints** — cada rota pública deve ter **pelo menos** um teste de integração que exercite **caminho feliz**; erros `4xx` principais devem ter **pelo menos** um caso coberto por rota crítica (`401`, `403`, `409` conforme aplicável).

### 23.3 Cobertura mínima (código)

- **`Parking.Application`:** cobertura de linhas **≥ 75%** (ferramenta: **Coverlet** + relatório em CI).  
- **`Parking.Api` (controllers / handlers):** cobertura de linhas **≥ 60%**, excluindo `Program.cs` e registros puramente de DI.  
- Se o projeto não atingir o percentual num **PR**, o **merge é bloqueado** até correção ou **exceção documentada** em ADR (Architecture Decision Record) com justificativa **e** plano de cobertura.

### 23.4 Definição de Pronto (DoD) — incremento entregue

Um incremento **só** é “concluído” e **pronto para uso em ambiente de desenvolvimento** quando **todas** as condições forem verdadeiras:

1. `dotnet test` na solução retorna **código 0** (todos os testes passam).  
2. Pipeline de CI (§23.6) está **verde** para o commit.  
3. Não há testes ignorados (`@Ignore` / `Skip`) **sem** issue vinculada e prazo — **proibido** merge com skip permanente silencioso.  
4. Migrações e seeds necessários estão aplicados ou documentados no `README` para o ambiente dev.  
5. Se o contrato HTTP mudou, **§18** (ou OpenAPI gerado) foi atualizado na mesma entrega.

**“Pronto para uso real” em dev** significa: Postgres real, API real, clientes apontando para ela — **não** JSON mockado no lugar do servidor.

### 23.5 O que a automação **não** garante

- Ausência total de defeitos em produção ou integrações com PSP **Production** não cobertas por sandbox.  
- Comportamento visual de apps (Web/Android) — **ver `SPEC_FRONTEND.md` §13**.

### 23.6 Integração contínua (CI)

**Obrigatório** repositório com pipeline (GitHub Actions, Azure DevOps, GitLab CI, etc.) que execute:

```bash
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
```

**Merge na branch principal** (`main` / `master`) **bloqueado** se qualquer etapa falhar. **Opcional recomendado:** limiar de cobertura falha o job se abaixo dos §23.3.

**Implementação normativa no repositório:** `.github/workflows/ci.yml` (deve executar os comandos acima ou equivalentes). Ver também **§25**.

### 23.7 Ambiente de teste e dados

- **Postgres:** Testcontainers **16** (imagem alinhada ao `docker-compose.yml`).  
- **Segredos em CI:** variáveis cifradas para `JWT_SECRET`, `PIX_WEBHOOK_SECRET` (valores fixos de teste, não produção).  
- **Não** usar banco de produção em testes automatizados.

---

## 24. Rastreabilidade spec ↔ testes

Cada suite de integração deve organizar testes por **área funcional** alinhada a esta spec (pastas ou traits): `Auth`, `Tickets`, `Checkout`, `Payments`, `Webhook`, `Packages`, `Cash`, `Dashboard`. Nome do teste deve referenciar o comportamento (ex.: `Checkout_ZeroAmount_ClosesTicketWithoutAwaitingPayment`).

---

## 25. Controles obrigatórios — zero entrega sem testes verdes

Esta secção **elimina** a possibilidade de “código entregue” sem suíte automatizada passando. **Normas técnicas** abaixo têm o mesmo peso que regras de negócio.

### 25.1 Definição normativa de “entregar”

**Entregar** código significa **apenas**: merge na branch principal (`main` / `master`) após **pull request** cujo **último commit** tenha passado em **todos** os trabalhos obrigatórios do ficheiro **`.github/workflows/ci.yml`** (ou equivalente migrado para outro fornecedor, mantendo os mesmos passos).

**Proibido:**

- Push **direto** em `main` / `master` (sem PR), exceto administrador de repositório em **hotfix** documentado em ADR — **fora do fluxo normal**.  
- Merge de PR com **checks falhados** ou **cinzentos** (skipped por erro de configuração).  
- Declarar tarefa “concluída” em documentação de release sem **URL do run de CI** verde anexado ou hash do commit.

### 25.2 Ficheiros obrigatórios no repositório

| Ficheiro | Função |
|----------|--------|
| `.github/workflows/ci.yml` | Pipeline única de verdade; **não** duplicar lógica de verificação só em documentação. |
| `.githooks/pre-commit` | Bloqueia `git commit` local se `dotnet test` falhar (quando existir `backend/*.sln`). |
| `AGENTS.md` | Regras para humanos e **agentes de IA** (incl. Cursor). |
| `.cursor/rules/tdd-entrega-zero-risco.mdc` | Regra **alwaysApply** — ver §25.4. |
| `docs/BRANCH_PROTECTION.md` | Passos para ativar **branch protection** no GitHub (não automatizáveis por ficheiro). |

### 25.3 Hooks Git locais (obrigatório para cada desenvolvedor)

Após clonar:

```bash
git config core.hooksPath .githooks
```

No Windows (PowerShell):

```powershell
git config core.hooksPath .githooks
```

O hook **`pre-commit`** executa `dotnet test` na solução em `backend/` quando **`backend/Parking.sln`** (ou qualquer `*.sln` sob `backend/`) existir. Se os testes falharem, o commit **é abortado**.

**Exceção:** commit com `--no-verify` **só** com aprovação escrita de dois maintainers em ADR — **proibido** no fluxo normal; mencionar na spec como **violação** se usado sem ADR.

### 25.4 Agentes de IA (Cursor e similares)

Devem obedecer integralmente a **`AGENTS.md`** e à regra **`.cursor/rules/tdd-entrega-zero-risco.mdc`**. Em particular: **nunca** afirmar “feito”, “pronto”, “concluído” sem **evidência** de comando `dotnet test` (e testes front quando aplicável) com **código de saída 0** no output da sessão, **ou** link para run de CI verde para o commit em causa.

### 25.5 Branch protection (GitHub)

Configuração obrigatória na UI do repositório (passos em `docs/BRANCH_PROTECTION.md`):

- **Require a pull request before merging**  
- **Require status checks to pass** — marcar o job `ci` (ou nome definido no workflow)  
- **Do not allow bypasses** para roles não administrativas  
- **Include administrators** (recomendado: também exigem CI)

### 25.6 O que ainda não é possível garantir por ficheiro

- **Comportamento de um humano** que use `--no-verify` ou desative branch protection — mitigado por revisão e política de equipa.  
- **Flaky tests** — mitigado por retries limitados no CI e proibição de testes instáveis sem issue.

---

## 26. Script de verificação local (opcional mas recomendado)

Antes de `git push`, executar **`scripts/verify.ps1`** (Windows) ou **`scripts/verify.sh`** (Unix) — deve invocar os mesmos passos que o CI (build + test). Se o script falhar, **não** abrir PR.

---

**Fim SPEC v8.7**
