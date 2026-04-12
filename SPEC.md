# SPEC CANÃ”NICA v8.7 â€” SISTEMA DE ESTACIONAMENTO ENTERPRISE (FECHAMENTO)

Documento **Ãºnico** de legitimidade do **backend**. Substitui v8.6 (stack Â§1.1 alinhada a .NET 10) e anteriores. **Frontend:** `SPEC_FRONTEND.md`.

---

## 0. Regra de execuÃ§Ã£o

Implementar **exatamente** o aqui descrito. **FORA DE ESCOPO** estÃ¡ na Â§1.

---

## 1. Escopo

**IncluÃ­do:** API backend, multi-banco, regras de negÃ³cio, antifraud mÃ­nimo, offline, webhook PIX via adaptador injetÃ¡vel, compra de pacote (CREDIT/PIX), stub de cartÃ£o.

**FORA DE ESCOPO:** layout Android/Web, ESC/POS, adquirente real de cartÃ£o, DPO/LGPD alÃ©m de L0, PCI formal.

---

## 1.1 Stack de implementaÃ§Ã£o do servidor (fechada)

| Item | Valor fixo |
|------|------------|
| **Runtime** | **.NET 10.0** (SDK estÃ¡vel alinhado ao repositÃ³rio; `TargetFramework` `net10.0`) |
| **Host** | ASP.NET Core **Web API** (minimal APIs ou controllers â€” **um** estilo por soluÃ§Ã£o; preferir **controllers** para rotas versionadas `/api/v1`). |
| **ORM** | **Entity Framework Core 10** + **Npgsql.EntityFrameworkCore.PostgreSQL** 10.x |
| **JSON** | `System.Text.Json` (padrÃ£o ASP.NET Core) |
| **Strings monetÃ¡rias na API** | Campos como `amount`, `price`, `price_per_hour`, totais de caixa: formato **`InvariantCulture`** com **`.`** decimal (ex.: `"10.50"`), independentemente da cultura do processo. |
| **Senhas** | biblioteca **Konscious.Security.Cryptography** (Argon2) ou binding para lib sodium â€” hash **PHC** conforme Â§3 |
| **JWT** | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| **HMAC webhook** | `HMACSHA256` sobre raw body |

**Estrutura de soluÃ§Ã£o (monorepo na raiz do repositÃ³rio `estacionamento`):**

```
estacionamento/
  backend/
    Parking.sln
    src/
      Parking.Api/              # Host, DI, middleware, Program.cs
      Parking.Application/      # Casos de uso, validaÃ§Ãµes de negÃ³cio
      Parking.Domain/           # Entidades puras (opcional mÃ­nimo)
      Parking.Infrastructure/   # EF DbContexts, IPaymentServiceProvider (PSP), tenants
  frontend-web/                 # Vue â€” ver SPEC_FRONTEND.md
  android/                      # Android â€” ver SPEC_FRONTEND.md
  database/
    seed/
      tenant_recharge_packages.sql
  docker-compose.yml
  .env.example
  README.md
```

**Nomes de assembly:** `Parking.Api`, `Parking.Application`, `Parking.Infrastructure`, `Parking.Domain`.

**Testes (obrigatÃ³rios â€” ver Â§23):** projeto `Parking.Tests` (xUnit) + integraÃ§Ã£o com **WebApplicationFactory** e **Testcontainers** (Postgres).

---

## 2. Arquitetura

### 2.1 Bancos

- `parking_identity` â€” usuÃ¡rios, refresh tokens.
- `parking_{uuid}` â€” tenant (nome fÃ­sico `parking_<uuid_minÃºsculo_sem_hÃ­fens>`).
- `parking_audit` â€” append-only.

**VariÃ¡veis:** `DATABASE_URL_IDENTITY`, `DATABASE_URL_AUDIT`, `TENANT_DATABASE_URL_TEMPLATE` com `{uuid}`.

**503:** `{ "code": "TENANT_UNAVAILABLE", "message": "string" }` se o banco do tenant nÃ£o conectar.

### 2.2 Tenant

- JWT: `parking_id` omitido se `SUPER_ADMIN`.
- `SUPER_ADMIN`: header **`X-Parking-Id: <uuid>`** obrigatÃ³rio; ignorar `parking_id` do JWT na resoluÃ§Ã£o do banco.

---

## 3. DDL â€” `parking_identity`

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

CREATE TABLE lojista_invites (
  id UUID PRIMARY KEY,
  parking_id UUID NOT NULL,
  lojista_id UUID NOT NULL,
  merchant_code VARCHAR(10) NOT NULL UNIQUE,
  activation_code_hash TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
  activated_at TIMESTAMPTZ,
  activated_user_id UUID
);
```

**Convites de lojista (`lojista_invites`):** criados por **ADMIN** ou **SUPER_ADMIN** do tenant; `merchant_code` Ã© alfanumÃ©rico **10** caracteres (Ãºnico global); `activation_code_hash` Ã© **SHA-256** hexadecimal (UTF-8) do cÃ³digo de ativaÃ§Ã£o em texto claro â€” **nÃ£o** armazenar o cÃ³digo de ativaÃ§Ã£o em claro. Cada convite referencia um `lojistas.id` jÃ¡ provisionado no banco do tenant (carteira zerada). ApÃ³s o primeiro `POST /auth/register-lojista` bem-sucedido, `activated_at` e `activated_user_id` sÃ£o preenchidos; reutilizar o convite retorna **409** `LOJISTA_INVITE_CONSUMED`.

**Integridade (backend ao criar/atualizar usuÃ¡rio):**

| role | parking_id | entity_id |
|------|------------|-----------|
| OPERATOR, MANAGER, ADMIN | NOT NULL | NULL |
| CLIENT | NOT NULL | = `clients.id` no tenant |
| LOJISTA | NOT NULL | = `lojistas.id` no tenant |
| SUPER_ADMIN | NULL | NULL |

**Senha:** Argon2id **m=19456, t=2, p=1**; salt 16 bytes aleatÃ³rios. **Armazenamento Ãºnico:** string **PHC** (`$argon2id$v=19$...`) no campo `password_hash TEXT` â€” **proibido** outro formato neste projeto.

---

## 4. DDL â€” `parking_audit`

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

**INSERT apenas.** Leitura: **SUPER_ADMIN**. Job diÃ¡rio: apagar `created_at < NOW() - interval '365 days'`.

---

## 5. DDL â€” `parking_{uuid}` (tenant)

```sql
CREATE TYPE ticket_status AS ENUM ('OPEN','AWAITING_PAYMENT','CLOSED');
CREATE TYPE payment_status AS ENUM ('PENDING','PAID','FAILED','EXPIRED');
CREATE TYPE payment_method AS ENUM ('PIX','CARD','CASH');
CREATE TYPE cash_session_status AS ENUM ('OPEN','CLOSED');

CREATE TABLE settings (
  id UUID PRIMARY KEY CHECK (id = '00000000-0000-0000-0000-000000000000'),
  price_per_hour NUMERIC(10,2) NOT NULL,
  capacity INT NOT NULL CHECK (capacity > 0),
  lojista_grant_same_day_only BOOLEAN NOT NULL DEFAULT FALSE
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
  hour_price NUMERIC(10,2) NOT NULL,
  allow_grant_before_entry BOOLEAN NOT NULL DEFAULT TRUE
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

-- BonificaÃ§Ãµes manuais: horas debitadas da carteira do lojista e creditadas ao cliente (por placa).
CREATE TABLE lojista_grants (
  id UUID PRIMARY KEY,
  lojista_id UUID NOT NULL,
  client_id UUID NOT NULL,
  plate VARCHAR(10) NOT NULL,
  hours INT NOT NULL CHECK (hours > 0),
  created_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
);
CREATE INDEX ix_lojista_grants_lojista_created ON lojista_grants(lojista_id, created_at);

CREATE TABLE wallet_usages (
  id UUID PRIMARY KEY,
  ticket_id UUID NOT NULL REFERENCES tickets(id),
  source TEXT NOT NULL CHECK (source IN ('lojista','client')),
  hours_used INT NOT NULL CHECK (hours_used > 0)
);

CREATE TABLE recharge_packages (
  id UUID PRIMARY KEY,
  display_name TEXT NOT NULL,
  scope TEXT NOT NULL CHECK (scope IN ('CLIENT','LOJISTA')),
  hours INT NOT NULL CHECK (hours > 0),
  price NUMERIC(10,2) NOT NULL CHECK (price >= 0),
  is_promo BOOLEAN NOT NULL DEFAULT FALSE,
  sort_order INT NOT NULL DEFAULT 0 CHECK (sort_order >= 0),
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

Normalizar: maiÃºsculas, remover espaÃ§os e hÃ­fens.

- Mercosul: `^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$`
- Legado: `^[A-Z]{3}[0-9]{4}$`

VÃ¡lido se **um** dos dois. SenÃ£o `400` `PLATE_INVALID`.

---

## 7. JWT e auth

- Access JWT **HS256**, claims: `iss=parking-identity`, `aud=parking-api`, `sub`=`user_id`, `role`, `parking_id` (omitir se null), `entity_id` (omitir se null), `iat`, `exp`; `exp = iat + 28800`.
- Refresh opaco; persistir **SHA-256** em `refresh_tokens.token_hash`; validade **30 dias**.
- Clock skew **Â±120s**.
- Login: mÃ¡x. **10** falhas / **15 min** / email â†’ `429` `LOGIN_THROTTLED`.
- `401` `OPERATOR_BLOCKED` se `operator_suspended=true` OU (`role=OPERATOR` e `PROBLEM` no dia UTC > 3).
- `POST /admin/operators/{user_id}/unsuspend` â€” **ADMIN** (tenant do usuÃ¡rio) ou **SUPER_ADMIN**; define `operator_suspended=false`.

---

## 8. Checkout â€” algoritmo completo (determinÃ­stico)

`POST /tickets/{id}/checkout` â€” header **`Idempotency-Key`** obrigatÃ³rio.

**IdempotÃªncia:** mesma chave + mesmo `ticket_id` â†’ **mesma** `response_json` 200 armazenada em `idempotency_store` (chave composta `Idempotency-Key` + rota normalizada).

**TransaÃ§Ã£o:**

1. `SELECT tickets WHERE id=:id FOR UPDATE`. Se nÃ£o existe â†’ `404`. Se `status != OPEN` â†’ `409` `INVALID_TICKET_STATE`.

2. `exit_time` = body `exit_time` (ISO8601) se presente, senÃ£o `NOW() AT TIME ZONE 'UTC'`. Se `exit_time < entry_time` â†’ `400` `VALIDATION_ERROR`.

3. `horas_total = CEIL(GREATEST(0, EXTRACT(EPOCH FROM (exit_time - entry_time)) / 3600))::int`

4. Carregar `settings` singleton. `price = price_per_hour`.

5. **Cliente por placa:** `client = SELECT * FROM clients WHERE plate = ticket.plate` (uma linha ou zero).

6. **`horas_lojista = 0`**, **`horas_cliente = 0`**.

7. **ConvÃªnio lojista (saldo bonificado da placa) â€” sempre antes da carteira comprada e antes de cobrar:**  
   - `horas_restantes = horas_total`.  
   - Se `settings.lojista_grant_same_day_only = false`: considerar **todas** as linhas em `lojista_grants` da placa.  
   - Se `settings.lojista_grant_same_day_only = true`: considerar somente `lojista_grants.created_at` dentro do **dia civil atual em `America/Sao_Paulo`**; na virada do dia o saldo bonificado disponÃ­vel passa a **0** sem apagar o histÃ³rico.  
   - `granted_plate = SUM(lojista_grants.hours WHERE plate = ticket.plate E janela vÃ¡lida acima)`. Se `granted_plate = 0` â†’ saldo bonificado = 0.  
   - `primeira_bonificacao_utc = MIN(lojista_grants.created_at WHERE plate = ticket.plate E janela vÃ¡lida acima)`.  
   - `used_plate = SUM(wallet_usages.hours_used JOIN tickets ON ticket_id WHERE source='lojista' AND tickets.plate = ticket.plate AND tickets.exit_time IS NOT NULL AND tickets.exit_time >= primeira_bonificacao_utc)` (consumos jÃ¡ contabilizados apÃ³s a primeira bonificaÃ§Ã£o na placa; alinha ao legado sem `lojista_grants`).  
   - `saldo_bonificado = MAX(0, granted_plate - used_plate)`.  
   - `horas_lojista = MIN(horas_restantes, saldo_bonificado)`.  
   - Se `horas_lojista > 0`: `INSERT wallet_usages(ticket_id, 'lojista', horas_lojista)`.  
   - `horas_restantes = horas_restantes - horas_lojista`.  
   - **Ordem fixa no checkout:** (1) horas bonificadas (convÃªnio) por placa; (2) horas da carteira comprada do cliente (`client_wallets`); (3) o restante em dinheiro/PIX/cartÃ£o (`amount`). **NÃ£o** depende de `clients.lojista_id` para existir saldo bonificado.

8. **Cliente (carteira comprada):**  
   - Se **nÃ£o existe** `client` â†’ `saldo_efetivo = 0`.  
   - SenÃ£o: `cw = SELECT * FROM client_wallets WHERE client_id = client.id`. Se **nÃ£o existe linha** â†’ `saldo_efetivo = 0`.  
   - SenÃ£o: se `cw.expiration_date IS NOT NULL` e `cw.expiration_date < NOW() AT TIME ZONE 'UTC'` â†’ `saldo_efetivo = 0`; senÃ£o `saldo_efetivo = cw.balance_hours`.  
   - `horas_cliente = MIN(horas_restantes, saldo_efetivo)`.  
   - Se `horas_cliente > 0`: `UPDATE client_wallets SET balance_hours = balance_hours - horas_cliente`; `INSERT wallet_usages(ticket_id, 'client', horas_cliente)`.

9. `horas_pagaveis = horas_total - horas_lojista - horas_cliente` (garantir â‰¥ 0).

10. `amount = ROUND(horas_pagaveis * price, 2)` com **half up** (equivalente `ROUND(numeric, 2)` no PostgreSQL).

11. **Se `amount = 0`:**  
    - `INSERT payments(ticket_id, method NULL, status PAID, amount 0, idempotency_key, paid_at = NOW() UTC)`.  
    - `UPDATE tickets SET status=CLOSED, exit_time=exit_time`.  
    - Audit `CHECKOUT` e `PAYMENT` (payloads Â§15).  
    - Resposta **200** (ver Â§18).

12. **Se `amount > 0`:**  
    - `INSERT payments(ticket_id, method NULL, status PENDING, amount, idempotency_key)`.  
    - `UPDATE tickets SET status=AWAITING_PAYMENT, exit_time=exit_time`.  
    - Audit `CHECKOUT`.  
    - Resposta **200** (ver Â§18).

### 8.1 RecÃ¡lculo com pagamento pendente (`AWAITING_PAYMENT`)

O mesmo `POST /tickets/{id}/checkout` com **nova** `Idempotency-Key` Ã© permitido quando o ticket estÃ¡ **`AWAITING_PAYMENT`** e existe **`payments`** com `ticket_id` desse ticket e **`status = PENDING`**. Caso contrÃ¡rio â†’ **409** `INVALID_TICKET_STATE`.

Antes de recalcular, o servidor **reverte** os consumos de carteira (`wallet_usages`) jÃ¡ associados a esse ticket e **reaplica** o algoritmo a partir do novo perÃ­odo.

**`exit_time` no corpo:** se **omitido**, usa-se o **instante atual UTC do servidor** (nÃ£o reaproveita a `exit_time` jÃ¡ gravada no ticket). Assim, se o condutor **permaneceu no pÃ¡tio** depois do primeiro checkout ou **nÃ£o concluiu** o pagamento, um novo checkout sem `exit_time` atualiza saÃ­da e valor. Se `exit_time` for enviada no JSON, ela prevalece (sujeita a validaÃ§Ã£o e a `X-Device-Time` quando aplicÃ¡vel).

**UI (Web/Android):** ao tocar **Pagar** ou ao abrir a tela de escolha de mÃ©todo para um `payment_id` de ticket, o cliente deve chamar esse checkout com corpo `{}` e **nova** chave de idempotÃªncia para alinhar valor ao instante atual.

---

## 9. PSP â€” provedor de pagamento (intercambiÃ¡vel)

Contrato Ãºnico **`IPaymentServiceProvider`** (`Parking.Infrastructure.Payments`): permite trocar PSP (Mercado Pago, EfÃ­, Stone, etc.) sem alterar `PaymentsController`, apenas registrando outra implementaÃ§Ã£o no DI.

**Pix â€” entrada lÃ³gica:** `payment_id`, `amount`, `expires_in_seconds` (default **1200** via `PIX_DEFAULT_TTL_SECONDS`).

**Pix â€” saÃ­da:** `qr_code`, `expires_at`, `provider_transaction_id` (gravados em `pix_transactions`).

**CartÃ£o:** `CardFlow = InPersonSimulated` (stub) ou `HostedCheckout` (ex.: Preference Mercado Pago; valor fixo vem do servidor; confirmaÃ§Ã£o via webhook do PSP).

**Webhook interno (stub / testes):** `PIX_WEBHOOK_SECRET` + HMAC em `POST /payments/webhook` (corpo JSON fixo Â§11).

### 9.1 VariÃ¡vel `PAYMENT_PSP`

| Valor | Comportamento |
|-------|----------------|
| **`Stub`** (padrÃ£o) | `StubPaymentServiceProvider`: Pix EMV simulada (`PIXSTUB|...`); cartÃ£o **sÃ­ncrono** em `POST /payments/card` (como antes). Sem HTTP externo. |
| **`MercadoPago`** | `MercadoPagoPaymentServiceProvider`: Pix via `POST https://api.mercadopago.com/v1/payments` (`MERCADOPAGO_ACCESS_TOKEN`); cartÃ£o via `POST /checkout/preferences`; confirmaÃ§Ã£o em `POST /payments/webhook/psp/mercadopago` (headers `x-signature`, `x-request-id`; segredo `MERCADOPAGO_WEBHOOK_SECRET`). |

**Compatibilidade:** se `PAYMENT_PSP` estiver vazio e `PIX_MODE=Production`, o host assume **`MercadoPago`** (substitui o antigo `ProductionPixProvider`).

**Mercado Pago (env):** `MERCADOPAGO_ACCESS_TOKEN`, `MERCADOPAGO_PUBLIC_KEY`, `MERCADOPAGO_WEBHOOK_SECRET`, `MERCADOPAGO_PAYER_EMAIL` (e opcionalmente `MERCADOPAGO_CHECKOUT_BACK_*_URL`, `MERCADOPAGO_API_BASE_URL`).

**Interface C# (referÃªncia):**

```csharp
public interface IPaymentServiceProvider
{
    string ProviderId { get; }
    CardPaymentFlow CardFlow { get; }
    Task<PixChargeResult> CreatePixChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct);
    Task<CardCheckoutSession> CreateCardCheckoutAsync(Guid paymentId, decimal amount, CancellationToken ct);
}
```

---

## 10. Pagamentos â€” PIX / cartÃ£o / dinheiro

**PrÃ©-condiÃ§Ã£o comum (ticket):** `ticket.status = AWAITING_PAYMENT`, existe `payment` com `ticket_id`, `status = PENDING`, `amount > 0`.

### POST /payments/pix `{ "payment_id": "uuid" }`

1. `SELECT payment FOR UPDATE`.  
2. Se `status = PAID` â†’ `409` `PAYMENT_ALREADY_PAID`.  
3. Se `status = EXPIRED` â†’ transiÃ§Ã£o **retry:** `UPDATE payments SET status=PENDING, failed_reason=NULL` (ticket continua `AWAITING_PAYMENT`).  
4. Se `package_order_id NOT NULL` â†’ prÃ©-condiÃ§Ã£o anÃ¡loga no pedido (`package_orders.status = AWAITING_PAYMENT`).  
5. `method = PIX` (UPDATE se NULL).  
6. Se existe `pix_transactions` com `active=true` e `expires_at > NOW() UTC` â†’ **200** com mesmo QR (resposta Â§18).  
7. Se existe ativo expirado: `active=false`.  
8. Chamar adaptador PIX; `INSERT pix_transactions` com `active=true`; demais `active=false` para esse `payment_id`.

### POST /payments/card `{ "payment_id", "amount" }`

Se `amount` â‰  `payment.amount` (comparaÃ§Ã£o decimal exata) â†’ `409` `AMOUNT_MISMATCH`.

- **PSP stub (`CardFlow = InPersonSimulated`):** `UPDATE payments SET method=CARD, status=PAID, paid_at=NOW() UTC`; fechar ticket / completar pacote como antes; audit `PAYMENT`. **Sem** `webhook_receipts`.
- **PSP checkout (`CardFlow = HostedCheckout`, ex. Mercado Pago):** `UPDATE payments SET method=CARD` mantendo `status=PENDING`; **200** com `mode=hosted_checkout`, `preference_id`, `init_point`, `sandbox_init_point`, `public_key` para o cliente abrir o checkout (valor jÃ¡ fixado na Preference). LiquidaÃ§Ã£o **apÃ³s** webhook do PSP (Â§11.1).

Em ambos: se `package_order_id NOT NULL`, prÃ©-condiÃ§Ãµes anÃ¡logas a `/payments/pix` (`AWAITING_PAYMENT`).

### POST /payments/cash `{ "payment_id" }`

PrÃ©: existe `cash_sessions` `OPEN` (Ãºnico). SenÃ£o `409` `CASH_SESSION_REQUIRED`.

`UPDATE payments SET method=CASH, status=PAID, paid_at=NOW() UTC`; `expected_amount += payment.amount` na sessÃ£o aberta.

- **Se `ticket_id` NOT NULL:** `UPDATE tickets SET status=CLOSED`. Audit `PAYMENT`.  
- **Se `package_order_id` NOT NULL:** mesmo bloco pacote que **CARD** acima.

---

## 11. Webhook `POST /payments/webhook`

**Sem JWT.**

Header: `X-Signature` = **hexadecimal minÃºsculo** de **HMAC-SHA256**(`PIX_WEBHOOK_SECRET`, **raw body** bytes UTF-8).

Body JSON exato (sem espaÃ§os extras se o cliente validar byte-a-byte; **recomendaÃ§Ã£o implementaÃ§Ã£o:** calcular HMAC sobre os bytes recebidos antes do parse):

```json
{ "transaction_id": "string", "payment_id": "uuid", "status": "PAID" }
```

**Processamento:**

1. Validar HMAC; senÃ£o `401` `WEBHOOK_SIGNATURE_INVALID`.  
2. Se `status != "PAID"` â†’ `400` `VALIDATION_ERROR`.  
3. Se `transaction_id` jÃ¡ em `webhook_receipts` â†’ **200** `{ "ok": true, "duplicate": true }`.  
4. Carregar `payment` com `FOR UPDATE`. Se nÃ£o existe â†’ `404`.  
5. Se `payment.status = PAID` â†’ **200** `{ "ok": true, "ignored": true }`.  
6. Se `payment.status = EXPIRED` ou `FAILED` â†’ **409** `WEBHOOK_LATE`.  
7. Se `payment.status != PENDING` â†’ **409** `INVALID_PAYMENT_STATE`.  
8. `UPDATE payments SET status=PAID, paid_at=NOW() UTC, method=COALESCE(method,'PIX')`.  
9. **Se `ticket_id` NOT NULL:** `UPDATE tickets SET status=CLOSED`.  
10. **Se `package_order_id` NOT NULL:** `UPDATE package_orders SET status=PAID, paid_at=NOW() UTC`; creditar `recharge_packages.hours` em `client_wallets` ou `lojista_wallets` (criar wallet com saldo 0 se nÃ£o existir â€” **INSERT** wallet com `balance_hours=hours` se novo); `INSERT wallet_ledger`; audit `PACKAGE_PURCHASE`.  
11. `INSERT webhook_receipts(transaction_id, payment_id)`.  
12. Audit `PAYMENT`.  
13. **200** `{ "ok": true }`.

### 11.1 Webhook Mercado Pago `POST /payments/webhook/psp/mercadopago/{parking_id}`

**Sem JWT.** O **tenant** vem do **path** (`parking_id` UUID), porque o Mercado Pago nÃ£o envia `X-Parking-Id`. (O header `X-Parking-Id` continua vÃ¡lido no webhook interno Â§11.)

Valida `x-signature` / `x-request-id` com `MERCADOPAGO_WEBHOOK_SECRET`; consulta `GET /v1/payments/{id}` na API MP; se `status=approved` e `external_reference` = `payment_id` interno e valor coincide (tolerÃ¢ncia **0,02**), marca `PAID` e replica efeitos do webhook interno (incl. `webhook_receipts` com `transaction_id` prefixo `mp:`). **Duplicados** e estados invÃ¡lidos: mesma semÃ¢ntica do Â§11.

---

## 12. ExpiraÃ§Ã£o PIX (job)

**A cada 60 segundos** (config `PIX_EXPIRY_JOB_SECONDS=60`):

Para cada `payments` com `status=PENDING` e (`method IS NULL` OR `method='PIX'`) e existir `pix_transactions` com `active=true` e `expires_at < NOW() UTC`:

- `UPDATE pix_transactions SET active=false WHERE id IN (...)`.
- `UPDATE payments SET status=EXPIRED, failed_reason='PIX_EXPIRED' WHERE id=:pid`.

**Ticket:** permanece `AWAITING_PAYMENT`. Novo pagamento: **`POST /payments/pix`** reativa `PENDING` conforme Â§10.

**Pacote:** `package_orders` permanece `AWAITING_PAYMENT`; mesmo retry via `/payments/pix`.

---

## 13. Compras de pacote

**POST /client/buy** â€” `Idempotency-Key` obrigatÃ³rio. Body `{ "package_id", "settlement": "CREDIT"|"PIX" }`.

- Validar pacote `active`, `scope=CLIENT`. `JWT.entity_id` = `clients.id` do tenant.

**CREDIT:** `package_orders` `PAID`, `paid_at=NOW()`, creditar horas, `wallet_ledger` `settlement=CREDIT`, audit. **Sem** linha em `payments`.

**PIX:** `package_orders` `AWAITING_PAYMENT`, `INSERT payments` `PENDING` com `package_order_id`, `amount=package.price`, `idempotency_key`; resposta com `payment_id`, `order_id`; cliente chama `POST /payments/pix`.

**POST /lojista/buy** â€” anÃ¡logo, `scope=LOJISTA`, `entity_id` = lojista.

**Wallet ausente ao creditar (pacote pago):** se nÃ£o existir `client_wallets`/`lojista_wallets`, **INSERT** com `balance_hours=0` antes de somar horas.

---

## 14. Antifraude e dashboard (UTC)

**Caixa ao fechar:** `divergencia = 0` se `expected=0`, senÃ£o `ABS(actual-expected)/expected`; se `> 0.05` â†’ `INSERT alerts` `CASH_DIVERGENCE`.

**ConvÃªnio:** numerador = COUNT DISTINCT `wallet_usages.ticket_id` JOIN `payments` ON â€¦ `source='lojista'`, `payments.status='PAID'`, `(paid_at AT TIME ZONE 'UTC')::date = D`. Denominador = COUNT `tickets` `CLOSED` com `(exit_time AT TIME ZONE 'UTC')::date = D`. `D = (NOW() AT TIME ZONE 'UTC')::date`. Se denom=0, nÃ£o calcular ratio; se `> 0.2` â†’ alerta `CONVENIO_RATIO`.

**Dashboard:** ver Â§18 `GET /dashboard`.

---

## 15. Auditoria â€” payload mÃ­nimo

| action | payload obrigatÃ³rio (campos) |
|--------|------------------------------|
| TICKET_CREATE | `ticket`: objeto ticket apÃ³s insert |
| CHECKOUT | `ticket_id`, `exit_time`, `hours_total`, `hours_lojista`, `hours_cliente`, `amount`, `payment_id` se houver |
| PAYMENT | `payment_id`, `from_status`, `to_status` |
| PACKAGE_PURCHASE | `order_id`, `package_id`, `settlement` |
| CASH_OPEN / CASH_CLOSE | `session_id`, `expected_amount`, `actual_amount` (quando aplicÃ¡vel) |
| ERROR_OFFLINE | `operation`, `code`, `idempotency_key` |
| TENANT_PROVISION | `parking_id`, `admin_user_id` |

**QR e segredos:** nÃ£o duplicar QR integral em audit; pode hash ou truncar.

---

## 16. Offline

Fila: `POST /tickets`, `POST /tickets/{id}/checkout` com `Idempotency-Key`. Proibido enfileirar `/payments/*`.

Se `exit_time` no body e `|device_now - server_now| > 300s` â†’ `400` `CLOCK_SKEW`.

---

## 17. RBAC â€” matriz por rota

Prefixo `/api/v1`. **401** se nÃ£o autenticado; **403** se autenticado sem permissÃ£o.

| Rota | OPERATOR | MANAGER | ADMIN | CLIENT | LOJISTA | SUPER_ADMIN |
|------|:--------:|:-------:|:-----:|:------:|:-------:|:-----------:|
| POST /auth/login, refresh, logout | âœ“ | âœ“ | âœ“ | âœ“ | âœ“ | âœ“ |
| POST /auth/register-lojista | â€” | â€” | â€” | â€” | â€” | â€” |
| POST /auth/register-client | â€” | â€” | â€” | â€” | â€” | â€” |
| GET /admin/lojista-invites, POST /admin/lojista-invites | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“* |
| POST /tickets | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /tickets/open, GET /tickets/{id} | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| POST /tickets/{id}/checkout | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /payments/{id} | âœ“ | âœ“ | âœ“ | âœ“Â° | âœ“Â° | âœ“* |
| POST /payments/pix,card,cash | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /recharge-packages | âœ— | âœ“ | âœ“ | âœ“Â°Â° | âœ“Â°Â° | âœ“* |
| GET /recharge-packages/manage | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“* |
| POST /recharge-packages | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“* |
| PUT /recharge-packages/{id} | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“* |
| DELETE /recharge-packages/{id} | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“* |
| GET /client/wallet, history, POST /client/buy | âœ— | âœ— | âœ— | âœ“ | âœ— | âœ— |
| GET /lojista/wallet, history, POST /lojista/buy | âœ— | âœ— | âœ— | âœ— | âœ“ | âœ— |
| GET /lojista/grant-settings, PUT /lojista/grant-settings | âœ— | âœ— | âœ— | âœ— | âœ“ | âœ— |
| POST /lojista/grant-client, GET /lojista/grant-client/history | âœ— | âœ— | âœ— | âœ— | âœ“ | âœ— |
| POST /cash/open, /cash/close, GET /cash | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /settings, POST /settings | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /dashboard | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /manager/movements | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /manager/analytics | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| GET /manager/balances-report | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| POST /operator/problem | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| POST /admin/operators/{id}/unsuspend | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“ |
| POST /admin/tenants | âœ— | âœ— | âœ— | âœ— | âœ— | âœ“ |

\*Requer `X-Parking-Id`.  
**â€”** `POST /auth/register-lojista` e `POST /auth/register-client`: **sem JWT** (rotas pÃºblicas); corpo vÃ¡lido cria utilizador e devolve o mesmo par de tokens que o login.  
**Â°** `GET /payments/{id}`: **CLIENT** apenas se o pagamento tiver `package_order_id` e o pedido for desse cliente (`package_orders.client_id = JWT.entity_id`). **LOJISTA** analogamente com `lojista_id`. Caso contrÃ¡rio **403** `FORBIDDEN`.  
**Â°Â°** `GET /recharge-packages`: **CLIENT** sÃ³ pode `scope=CLIENT`. **LOJISTA** sÃ³ `scope=LOJISTA`. **MANAGER/ADMIN/SUPER_ADMIN** podem qualquer `scope`. ViolaÃ§Ã£o â†’ **403**.

**POST /payments/webhook:** **nÃ£o** usa JWT. AutenticaÃ§Ã£o **somente** `X-Signature` (HMAC). Nenhuma coluna RBAC aplica-se; allowlist de IP Ã© **FORA DE ESCOPO**.

---

## 18. Contratos HTTP completos

**Erro padrÃ£o:** `{ "code": "<CODE>", "message": "<string>" }`.

**CÃ³digos fechados:**  
`VALIDATION_ERROR`, `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `CONFLICT`, `PLATE_INVALID`, `PLATE_HAS_ACTIVE_TICKET`, `INVALID_TICKET_STATE`, `LOJISTA_WALLET_MISSING`, `PAYMENT_ALREADY_PAID`, `AMOUNT_MISMATCH`, `CASH_SESSION_REQUIRED`, `OPERATOR_BLOCKED`, `TENANT_UNAVAILABLE`, `LOGIN_THROTTLED`, `WEBHOOK_SIGNATURE_INVALID`, `WEBHOOK_LATE`, `INVALID_PAYMENT_STATE`, `CLOCK_SKEW`, `LOJISTA_INVITE_INVALID`, `LOJISTA_INVITE_CONSUMED`, `LOJISTA_CREDIT_INSUFFICIENT`, `CLIENT_FOR_OTHER_LOJISTA`, `GRANT_REQUIRES_ACTIVE_TICKET`, `INTERNAL`.

### POST /auth/login

Request: `{ "email": "a@b.com", "password": "..." }`  
Response **200:** `{ "access_token": "jwt", "refresh_token": "opaco", "expires_in": 28800 }`

### POST /auth/refresh

Request: `{ "refresh_token": "..." }`  
Response **200:** igual login (novo par).

### POST /auth/logout

Request: `{ "refresh_token": "..." }`  
Response **200:** `{ "ok": true }`

### POST /auth/register-lojista

**Sem JWT.** Auto cadastro de **LOJISTA** com convite emitido pelo gestor.

Request: `{ "merchantCode": "string10", "activationCode": "string", "email": "...", "password": "...", "name": "Nome da loja" }`  
Response **200:** igual `POST /auth/login` (`access_token`, `refresh_token`, `expires_in`).  
Erros: **400** `LOJISTA_INVITE_INVALID` (cÃ³digo inexistente ou ativaÃ§Ã£o incorreta â€” mesma mensagem genÃ©rica); **409** `LOJISTA_INVITE_CONSUMED` (convite jÃ¡ utilizado); **409** `CONFLICT` se `e-mail` jÃ¡ existir em `users`.

### POST /auth/register-client

**Sem JWT.** Auto cadastro de **CLIENT** no tenant informado.

Request: `{ "parkingId": "uuid", "plate": "ABC1D23", "email": "...", "password": "..." }`  
Response **200:** igual `POST /auth/login` (`access_token`, `refresh_token`, `expires_in`).  
Efeitos: `INSERT` em `clients` (placa normalizada, `lojista_id = null`) e `client_wallets` com saldo inicial **0**, depois `INSERT` em `users` com `role = CLIENT`, `parking_id` do corpo e `entity_id = clients.id`.  
Erros: **400** `VALIDATION_ERROR` (campos obrigatÃ³rios / `parkingId` vazio), **400** `PLATE_INVALID`, **404** `NOT_FOUND` (estacionamento inexistente), **409** `CONFLICT` se `e-mail` jÃ¡ existir em `users` ou a `plate` jÃ¡ existir no tenant.

### POST /admin/lojista-invites

**ADMIN** ou **SUPER_ADMIN**\*; requer tenant resolvido (`parking_id` no JWT ou `X-Parking-Id` para super).

Request (opcional): `{ "displayName": "Nome da loja" }` â€” se omitido ou vazio, usado nome padrÃ£o **Â«Lojista pendenteÂ»**.

Efeitos: `INSERT` em `lojistas` e `lojista_wallets` (saldo **0**, `hour_price` **0** no tenant); `INSERT` em `lojista_invites` com cÃ³digos gerados.

Response **201:** `{ "merchantCode": "10chars", "activationCode": "texto claro sÃ³ nesta resposta", "lojistaId": "uuid" }` â€” o cliente deve persistir o `activationCode` de forma segura; o servidor sÃ³ guarda o hash.

### GET /admin/lojista-invites

**ADMIN** ou **SUPER_ADMIN**\*.

Response **200:** lista **todos** os registos em `lojistas` do tenant (nÃ£o sÃ³ convites recentes), enriquecida com dados de identidade e carteira.

Cada item:

| Campo | Significado |
|--------|-------------|
| `merchantCode` | CÃ³digo pÃºblico de 10 caracteres, ou `null` se o lojista existir sem linha de convite (ex.: dados de teste). |
| `lojistaId` | UUID do lojista no tenant. |
| `shopName` | Nome da loja (`lojistas.name`). |
| `createdAt` | Data de criaÃ§Ã£o do convite, ou `null` se nÃ£o houver convite. |
| `activated` | `true` se o convite foi consumido **ou** se existir utilizador **LOJISTA** com `entity_id` = `lojistaId`. |
| `email` | E-mail da conta lojista quando `activated`; caso contrÃ¡rio `null`. |
| `totalPurchasedHours` | Soma das horas de encomendas `package_orders` com `scope=LOJISTA`, `status=PAID`; `null` se **nÃ£o** `activated`. |
| `balanceHours` | Saldo em `lojista_wallets.balance_hours`; `null` se **nÃ£o** `activated`. |

**Sem** expor cÃ³digo de ativaÃ§Ã£o. OrdenaÃ§Ã£o: `createdAt` do convite descendente (sem convite por Ãºltimo), depois `shopName`.

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
  "payment": <PaymentDTO> | null,
  "lojistaBenefits": [
    {
      "lojistaId": "uuid",
      "lojistaName": "string",
      "hoursAvailable": 0,
      "hoursGrantedTotal": 0
    }
  ]
}
```

`PaymentDTO` **idÃªntico** ao de `GET /payments/{id}` quando existir pagamento para o ticket; senÃ£o `null`.

`lojistaBenefits`: array (pode ser vazio). Cada elemento corresponde a um lojista que concedeu bonificaÃ§Ã£o Ã  placa do ticket e tem **saldo bonificado disponÃ­vel na saÃ­da** (`hoursAvailable` &gt; 0); inclui **nome**, **id** do lojista e **total de horas concedidas** ao longo do tempo para aquela placa (`hoursGrantedTotal`). Lojistas sem saldo disponÃ­vel nÃ£o aparecem. A ordem dos elementos nÃ£o implica repartiÃ§Ã£o entre lojistas no checkout; o dÃ©bito de convÃªnio usa o **saldo agregado por placa** (passo 7), sempre **antes** da carteira comprada.

### GET /payments/{id}

**Leitura** para polling de PIX e conferÃªncia de estado. Regras **Â°** na matriz RBAC.

Nota de integraÃ§Ã£o UI: clientes Web/Android devem tratar `status` de forma case-insensitive e, ao voltar do app bancÃ¡rio (foco/foreground), disparar leitura imediata para nÃ£o manter a tela PIX travada apÃ³s pagamento confirmado.

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

`pix`: se existir `pix_transactions` com `active=true` para este pagamento, entÃ£o  
`{ "expires_at": "ISO8601", "active": true }`; caso contrÃ¡rio `null`.

### GET /settings

Roles: **MANAGER**, **ADMIN**, **SUPER_ADMIN**\*.

Response **200:** `{ "price_per_hour": "5.00", "capacity": 50, "lojista_grant_same_day_only": false }` (valores exemplares; refletem o tenant).

### GET /settings/audit

Roles: **MANAGER**, **ADMIN**, **SUPER_ADMIN**\*.

Response **200:** `{ "items": [{ "id", "created_at", "actor_user_id", "actor_email", "actor_role", "changes": [{ "field", "label", "from", "to" }] }] }`

Retorna apenas eventos `SETTINGS_UPDATE` do estacionamento atual, ordenados do mais recente para o mais antigo. Cada item informa **quem** alterou, **quando** alterou e os campos alterados com valores **antes/depois**.

### GET /recharge-packages

Query **obrigatÃ³ria:** `scope=CLIENT` ou `scope=LOJISTA` (regras **Â°Â°**).

Response **200:** `{ "items": [ { "id", "display_name", "scope", "hours", "price", "is_promo", "sort_order" } ] }` â€” somente pacotes `active=true`.

### GET /recharge-packages/manage

Roles: **ADMIN**, **SUPER_ADMIN**\*.

Query **obrigatÃ³ria:** `scope=CLIENT` ou `scope=LOJISTA`.

Response **200:** `{ "items": [ { "id", "display_name", "scope", "hours", "price", "is_promo", "sort_order", "active" } ] }`

### POST /recharge-packages

Roles: **ADMIN**, **SUPER_ADMIN**\*.

Request:

```json
{
  "displayName": "Cliente Promo 30h",
  "scope": "CLIENT",
  "hours": 30,
  "price": 99.90,
  "isPromo": true,
  "sortOrder": 15,
  "active": true
}
```

Response **201:** `{ "id", "display_name", "scope", "hours", "price", "is_promo", "sort_order", "active" }`

### PUT /recharge-packages/{id}

Roles: **ADMIN**, **SUPER_ADMIN**\*.

Mesmo corpo do `POST /recharge-packages`.

Response **200:** `{ "id", "display_name", "scope", "hours", "price", "is_promo", "sort_order", "active" }`

### DELETE /recharge-packages/{id}

Roles: **ADMIN**, **SUPER_ADMIN**\*.

Remove o pacote **somente** quando ele ainda nÃ£o tiver sido usado em pedidos ou lanÃ§amentos de carteira.

Response **200:** `{ "ok": true }`

Erros:

- **404** `NOT_FOUND` se o pacote nÃ£o existir.
- **409** `PACKAGE_IN_USE` se o pacote jÃ¡ participou de compra/histÃ³rico; neste caso deve ser apenas desativado.

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

Query: `limit` default 50 mÃ¡x 100, `cursor` opcional **opaque** (base64url de `created_at|id`).  
Response **200:** `{ "items": [ { "id", "kind": "PURCHASE|USAGE", "delta_hours", "amount", "created_at", "ref": { "type": "ticket|package", "id": "uuid" } } ], "next_cursor": null | "string" }`

**USAGE:** de `wallet_usages` join `tickets` onde `clients.plate = tickets.plate` do JWT client.  
**PURCHASE:** de `wallet_ledger` para o `client_id`.

### GET /lojista/wallet | history

AnÃ¡logo; USAGE pode ser vazio se nÃ£o houver por ticket.

### GET /lojista/grant-settings

**LOJISTA.**

Resposta **200:** `{ "allow_grant_before_entry": true | false }`  
- `true` (padrÃ£o): permite bonificar sÃ³ com **placa** (crÃ©dito antecipado), sem exigir ticket no estacionamento.  
- `false`: sÃ³ permite bonificar se existir ticket **`OPEN`** ou **`AWAITING_PAYMENT`** para a placa (bonificaÃ§Ã£o por **placa**), ou se `ticketId` referir um ticket nesses estados.

### PUT /lojista/grant-settings

**LOJISTA.**

Request: `{ "allow_grant_before_entry": true | false }`  
Resposta **200:** corpo igual ao GET (valor persistido).

### POST /lojista/grant-client

**LOJISTA.** Bonifica por placa/ticket: debita horas da carteira de convÃªnio do lojista e acumula saldo bonificado do convÃªnio para a placa (separado da carteira comprada do cliente).

Quando `lojistas.allow_grant_before_entry = false`: antes de debitar, o servidor exige estadia ativa â€” existe ticket com a placa resolvida em estado **`OPEN`** ou **`AWAITING_PAYMENT`**, ou o `ticketId` enviado estÃ¡ nesse estado. Caso contrÃ¡rio **409** `GRANT_REQUIRES_ACTIVE_TICKET`.

Header: `Idempotency-Key` (obrigatÃ³rio). Rota de idempotÃªncia: `POST /lojista/grant-client`.

Request (JSON camelCase): `{ "plate": "ABC1D23" | null, "ticketId": "uuid" | null, "hours": n | null }` â€” deve existir **plate** ou **ticketId**; se `hours` omitido ou &lt; 1, usa **1**; mÃ¡ximo **720**.

Resposta **200:** `{ "grant_id", "plate", "hours", "client_balance_hours", "lojista_balance_hours" }`.

`client_balance_hours` neste endpoint representa o **saldo bonificado disponÃ­vel da placa no convÃªnio** (nÃ£o a carteira comprada do cliente), **jÃ¡ incluindo** a bonificaÃ§Ã£o recÃ©m-registada â€” inclusive quando jÃ¡ existia linha em `clients` para essa placa (conta/cadastro do cliente).

Erros: **400** `VALIDATION_ERROR` / `PLATE_INVALID`; **404** `NOT_FOUND` (ticket inexistente); **409** `LOJISTA_CREDIT_INSUFFICIENT` (saldo de horas do lojista insuficiente); **409** `CLIENT_FOR_OTHER_LOJISTA` (placa jÃ¡ vinculada a outro lojista); **409** `GRANT_REQUIRES_ACTIVE_TICKET` (modo â€œsÃ³ no pÃ¡tioâ€ e sem ticket ativo para a placa / ticket encerrado). RequisiÃ§Ã£o repetida com a mesma chave devolve o mesmo JSON gravado na loja de idempotÃªncia.

### GET /lojista/grant-client/history

**LOJISTA.** Extrato das bonificaÃ§Ãµes concedidas pelo lojista autenticado.

Query (opcional): `from`, `to` (ISO8601, comparado a `created_at`), `plate` (normalizada se vÃ¡lida), `limit` (1â€“200, default 100).

Resposta **200:** `{ "items": [ { "id", "created_at", "plate", "hours", "grant_mode", "client_id" } ] }` ordenado por data descendente.`r`n`r`n`grant_mode`: `ON_SITE` (bonificacao com veiculo no patio / ticket ativo) ou `ADVANCE` (credito antecipado).

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

Request: `{ "price_per_hour": "10.00", "capacity": 100, "lojista_grant_same_day_only": true }`  
Response **200:** `{ "ok": true }`

`price_per_hour` e `capacity` continuam editÃ¡veis por **MANAGER**, **ADMIN** e **SUPER_ADMIN**\*. JÃ¡ `lojista_grant_same_day_only` sÃ³ pode ser alterado por **ADMIN** e **SUPER_ADMIN**\*; se um **MANAGER** tentar enviar este campo, a API devolve **403** `FORBIDDEN`.

Sempre que houver alteraÃ§Ã£o efetiva em qualquer configuraÃ§Ã£o, gravar `parking_audit.audit_events` com `entity_type = "settings"`, `action = "SETTINGS_UPDATE"` e payload contendo o ator (`actor_user_id`, `actor_email`, `actor_role`) e a lista `changes[]` com `field`, `label`, `from` e `to`.

### POST /operator/problem

Request: `{}`  
Response **200:** `{ "ok": true }`

### POST /admin/operators/{user_id}/unsuspend

Response **200:** `{ "ok": true }`

### POST /admin/tenants

**Somente SUPER_ADMIN.** O papel **ADMIN** (administrador do tenant) **nÃ£o** pode criar estacionamento novo; acede apenas ao `parking_id` do seu utilizador. Request (JSON camelCase tÃ­pico da API):

```json
{
  "parkingId": "uuid opcional â€” se omitido servidor gera",
  "adminEmail": "admin@estacionamento.com",
  "adminPassword": "SenhaForte123!",
  "operatorEmail": "operador@estacionamento.com",
  "operatorPassword": "OutraSenha123!"
}
```

Regras: `operatorEmail` e `operatorPassword` obrigatÃ³rios; `adminEmail` e `operatorEmail` devem ser **distintos** (normalizaÃ§Ã£o case-insensitive).  
Erros: `400` `VALIDATION_ERROR` (campos em falta ou e-mails iguais); `409` `CONFLICT` se qualquer e-mail jÃ¡ existir em **identity**.

Efeitos:

1. Se `parkingId` omitido, gerar UUID v4.  
2. `CREATE DATABASE parking_<uuid_sem_hifen>`.  
3. Rodar todas as migrations do tenant na ordem `schema_migrations`.  
4. `INSERT settings` singleton `price_per_hour=5.00`, `capacity=50` (defaults fixos).  
5. `INSERT` em **identity** (transaÃ§Ã£o): utilizador **ADMIN** do tenant e utilizador **OPERATOR** do mesmo `parking_id`, ambos com `password_hash` Argon2id e `active=true`.  
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

### GET /manager/movements

Query opcional: `from`, `to`, `kind`, `lojista_id`, `limit`.

Response **200:** `{ "from", "to", "count", "insights", "items" }`

- `insights`: `total_ticket`, `total_package`, `usages_lojista`, `usages_client`.
- `items` inclui composição do ticket quando `kind=TICKET_PAYMENT`:
  - `ticket_split_type`: `CLIENT_DIRECT_ONLY` | `CLIENT_WALLET_ONLY` | `LOJISTA_ONLY` | `MIXED`
  - `hours_lojista`, `hours_cliente`, `hours_direct`
  - `lojista_id` quando houver vínculo.

Filtro `lojista_id`: restringe extrato a movimentos ligados ao lojista informado (usos de convênio, pagamentos de pacote lojista e tickets vinculados).

### GET /manager/analytics

Query: `days` (1..90). Response **200:** tendências e picos (`days`, `totals`, `trend_by_day`, `gains_by_hour`, `peak_hours`) em JSON camelCase.

### GET /manager/balances-report

**Roles:** MANAGER, ADMIN, SUPER_ADMIN (tenant via JWT; SUPER_ADMIN com `X-Parking-Id`).

Query opcional: `plate` — se informado, normaliza como placa; se inválido após normalização, **400** `VALIDATION_ERROR`. Filtra por substring na placa normalizada as listas `clientPlates` e `lojistaBonificadoPlates`.

Response **200:**

```json
{
  "lojistas": [{ "lojistaId": "uuid", "lojistaName": "string", "balanceHours": 0 }],
  "lojistaBonificadoPlates": [{ "plate": "string", "balanceHours": 0 }],
  "clientPlates": [{ "plate": "string", "balanceHours": 0, "expirationDate": "RFC3339 ou null" }]
}
```

- `lojistas`: todos os lojistas do tenant; `balanceHours` da carteira convênio (0 se sem registo); ordenação: maior saldo primeiro, depois nome.
- `lojistaBonificadoPlates`: apenas placas com horas **bonificadas por lojistas** (`lojista_grants`) ainda **disponíveis** &gt; 0 — mesma regra de saldo que o checkout (`PlateAvailableBonificadoHoursAsync`); não inclui linhas com saldo 0 (ex.: já consumido em saídas após a bonificação). Ordenação: maior `balanceHours` primeiro, depois placa. Com `plate` na query, apenas placas que contêm o filtro normalizado.
- `clientPlates`: clientes do tenant; `balanceHours` efetivo da carteira **comprada** (0 se carteira expirada ou inexistente, alinhado ao checkout); `expirationDate` da carteira ou `null`; ordenação: maior `balanceHours` primeiro, depois placa. Com `plate` na query, apenas linhas cuja placa contém o filtro normalizado.

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

DefiniÃ§Ãµes (`D` = `(NOW() AT TIME ZONE 'UTC')::date`):

- `faturamento` = `SUM(amount)` de `payments` `PAID` com `(paid_at AT TIME ZONE 'UTC')::date = D`.  
- `ocupacao` = `COUNT(*) FILTER (WHERE status='OPEN')::numeric / capacity` (capacity de `settings`).  
- `tickets_dia` = COUNT `tickets` com `(exit_time AT TIME ZONE 'UTC')::date = D` e `exit_time IS NOT NULL`.  
- `uso_convenio` = numerador/denominador Â§14 ou `null` se denom=0.

### GET /health

**200:** `{ "ok": true, "serverTimeUtc": "<DateTimeOffset RFC 3339 UTC>" }` — `serverTimeUtc` Ã© o relÃ³gio do **servidor** (UTC). Clientes Web/Android, **com internet**, devem comparar com o relÃ³gio do dispositivo: **data civil** em **America/Sao_Paulo** deve ser **igual** nos dois lados e a diferenÃ§a de instante deve ser **â‰¤ 5 minutos**; caso contrÃ¡rio o cliente fica **inoperante** com mensagem a indicar ajuste de data/hora (ver SPEC_FRONTEND). **Sem internet**, o cliente nÃ£o aplica este bloqueio e usa o relÃ³gio local para exibiÃ§Ã£o.

---

## 19. Docker e ambiente local

**Arquivo normativo na raiz:** `docker-compose.yml` â€” **PostgreSQL 16** exposto em **`localhost:5432`**, usuÃ¡rio/senha **`parking` / `parking_dev`**, volume nomeado para dados.

A API **nÃ£o** precisa estar no Compose na v1; desenvolvedor roda `dotnet run` em `backend/src/Parking.Api` apontando para o Postgres do Compose.

**VariÃ¡veis:** ver **`.env.example`** na raiz (copiar para `.env` e ajustar). Lista mÃ­nima:

- `DATABASE_URL_IDENTITY` â€” Npgsql connection string para banco `parking_identity`  
- `DATABASE_URL_AUDIT` â€” idem `parking_audit`  
- `TENANT_DATABASE_URL_TEMPLATE` â€” `Host=localhost;Port=5432;Username=parking;Password=parking_dev;Database=parking_{uuid};` (formato exato pode usar **Npgsql** key-value; `{uuid}` **sem hÃ­fens**)  
- `JWT_SECRET` â€” mÃ­nimo 32 caracteres aleatÃ³rios  
- `PIX_WEBHOOK_SECRET` â€” mÃ­nimo 32 caracteres  
- `PAYMENT_PSP` â€” `Stub` ou `MercadoPago` (opcional; vazio + `PIX_MODE=Production` â†’ Mercado Pago)  
- `PIX_MODE` â€” legado: `Stub` ou `Production` (sÃ³ usado se `PAYMENT_PSP` vazio)  
- `CORS_ORIGINS` â€” ex.: `http://localhost:5173`  
- `ASPNETCORE_URLS` â€” `http://0.0.0.0:8080`

**CriaÃ§Ã£o dos bancos globais:** script SQL Ãºnico **`database/init/00_create_databases.sql`** (criar `parking_identity`, `parking_audit` vazios) executado **uma vez** contra o Postgres do Compose antes da primeira migraÃ§Ã£o EF.

---

## 20. MÃ¡quinas de estado (resumo)

- Ticket: `OPEN` â†’ `AWAITING_PAYMENT` â†’ `CLOSED`; exceÃ§Ã£o checkout `amount=0` â†’ direto `CLOSED`.  
- Payment: `PENDING` â†’ `PAID` | `FAILED` | `EXPIRED`; retry `EXPIRED` â†’ `PENDING` ao chamar `/payments/pix`.  
- `package_order` PIX: `AWAITING_PAYMENT` â†’ `PAID` | `FAILED` | `CANCELLED`.  
- Cash: um `OPEN` por tenant.

---

## 21. MigraÃ§Ãµes e seed

1. **EF Core 10:** trÃªs contextos ou **um** contexto identity + audit + factory para tenant â€” **obrigatÃ³rio** aplicar migrations na ordem numÃ©rica em `schema_migrations`.  
2. **ApÃ³s** existir pelo menos **um** tenant (`POST /admin/tenants`), executar no banco **`parking_{uuid}`** o arquivo **`database/seed/tenant_recharge_packages.sql`** (pacotes de exemplo CLIENT/LOJISTA). Pode ser automatizado no provisionamento (passo extra apÃ³s migrations do tenant).  
3. **Operador inicial:** o `POST /admin/tenants` cria tambÃ©m o **primeiro operador** do tenant (e-mail e senha no corpo).

---

## 22. Identificadores Android (referÃªncia de repositÃ³rio)

- **`applicationId`:** `com.estacionamento.parking`  
- **Namespace Kotlin:** `com.estacionamento.parking`

*(Detalhe em `SPEC_FRONTEND.md` Â§1.3.)*

---

## 23. Qualidade, TDD e definiÃ§Ã£o de pronto

Esta secÃ§Ã£o Ã© **normativa**. O objetivo Ã© que o sistema **sÃ³ seja considerado utilizÃ¡vel** quando a automaÃ§Ã£o demonstrar o comportamento esperado â€” **sem depender de teste manual** do product owner para validar cada entrega (aceite substituÃ­do por **suÃ­te verde + DoD**).

### 23.1 TDD (ciclo obrigatÃ³rio por funcionalidade)

Para cada **nova** regra de negÃ³cio ou **novo** endpoint:

1. Escrever **teste automatizado** que **falhe** (red) â€” nÃ­vel adequado: unitÃ¡rio (domÃ­nio) ou integraÃ§Ã£o (API).  
2. Implementar o **mÃ­nimo** para o teste passar (green).  
3. Refatorar mantendo os testes verdes (refactor).

**ExceÃ§Ã£o controlada:** correÃ§Ã£o de bug de produÃ§Ã£o pode comeÃ§ar por teste de regressÃ£o que reproduz o bug, depois fix â€” equivalente TDD.

### 23.2 PirÃ¢mide de testes â€” backend

| Camada | Escopo | Ferramentas de referÃªncia (.NET 10) |
|--------|--------|-------------------------------------|
| **Unit** | Regras puras (cÃ¡lculo de horas, arredondamento, validaÃ§Ãµes sem I/O) | xUnit, asserÃ§Ãµes em `Parking.Application` |
| **IntegraÃ§Ã£o** | HTTP real â†’ API â†’ Postgres real (**container**) | `WebApplicationFactory`, **Testcontainers.PostgreSQL**, `HttpClient` |
| **Contrato** | Status HTTP, shape JSON dos erros `{ code, message }`, headers `Authorization` / `Idempotency-Key` | Mesma base de integraÃ§Ã£o; opcional snapshot de JSON |
| **E2E API** (fluxo mÃ­nimo) | Cadeia completa em ambiente **dev** com `PIX_MODE=Stub` | Um teste (ou poucos) que: provisiona tenant (ou usa fixture), cria usuÃ¡rio operador, **login**, **POST /tickets**, **checkout**, **POST /payments/pix**, **POST /payments/webhook** com HMAC vÃ¡lido, assert ticket `CLOSED` |

**Proibido** substituir integraÃ§Ã£o de API por apenas mocks do DbContext para **endpoints** â€” cada rota pÃºblica deve ter **pelo menos** um teste de integraÃ§Ã£o que exercite **caminho feliz**; erros `4xx` principais devem ter **pelo menos** um caso coberto por rota crÃ­tica (`401`, `403`, `409` conforme aplicÃ¡vel).

### 23.3 Cobertura mÃ­nima (cÃ³digo)

- **`Parking.Application`:** cobertura de linhas **â‰¥ 75%** (ferramenta: **Coverlet** + relatÃ³rio em CI).  
- **`Parking.Api` (controllers / handlers):** cobertura de linhas **â‰¥ 60%**, excluindo `Program.cs` e registros puramente de DI.  
- Se o projeto nÃ£o atingir o percentual num **PR**, o **merge Ã© bloqueado** atÃ© correÃ§Ã£o ou **exceÃ§Ã£o documentada** em ADR (Architecture Decision Record) com justificativa **e** plano de cobertura.

### 23.4 DefiniÃ§Ã£o de Pronto (DoD) â€” incremento entregue

Um incremento **sÃ³** Ã© â€œconcluÃ­doâ€ e **pronto para uso em ambiente de desenvolvimento** quando **todas** as condiÃ§Ãµes forem verdadeiras:

1. `dotnet test` na soluÃ§Ã£o retorna **cÃ³digo 0** (todos os testes passam).  
2. Pipeline de CI (Â§23.6) estÃ¡ **verde** para o commit.  
3. NÃ£o hÃ¡ testes ignorados (`@Ignore` / `Skip`) **sem** issue vinculada e prazo â€” **proibido** merge com skip permanente silencioso.  
4. MigraÃ§Ãµes e seeds necessÃ¡rios estÃ£o aplicados ou documentados no `README` para o ambiente dev.  
5. Se o contrato HTTP mudou, **Â§18** (ou OpenAPI gerado) foi atualizado na mesma entrega.

**â€œPronto para uso realâ€ em dev** significa: Postgres real, API real, clientes apontando para ela â€” **nÃ£o** JSON mockado no lugar do servidor.

### 23.5 O que a automaÃ§Ã£o **nÃ£o** garante

- AusÃªncia total de defeitos em produÃ§Ã£o ou integraÃ§Ãµes com PSP **Production** nÃ£o cobertas por sandbox.  
- Comportamento visual de apps (Web/Android) â€” **ver `SPEC_FRONTEND.md` Â§13**.

### 23.6 IntegraÃ§Ã£o contÃ­nua (CI)

**ObrigatÃ³rio** repositÃ³rio com pipeline (GitHub Actions, Azure DevOps, GitLab CI, etc.) que execute:

```bash
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
```

**Merge na branch principal** (`main` / `master`) **bloqueado** se qualquer etapa falhar. **Opcional recomendado:** limiar de cobertura falha o job se abaixo dos Â§23.3.

**ImplementaÃ§Ã£o normativa no repositÃ³rio:** `.github/workflows/ci.yml` (deve executar os comandos acima ou equivalentes). Ver tambÃ©m **Â§25**.

### 23.7 Ambiente de teste e dados

- **Postgres:** Testcontainers **16** (imagem alinhada ao `docker-compose.yml`).  
- **Segredos em CI:** variÃ¡veis cifradas para `JWT_SECRET`, `PIX_WEBHOOK_SECRET` (valores fixos de teste, nÃ£o produÃ§Ã£o).  
- **NÃ£o** usar banco de produÃ§Ã£o em testes automatizados.

---

## 24. Rastreabilidade spec â†” testes

Cada suite de integraÃ§Ã£o deve organizar testes por **Ã¡rea funcional** alinhada a esta spec (pastas ou traits): `Auth`, `Tickets`, `Checkout`, `Payments`, `Webhook`, `Packages`, `Cash`, `Dashboard`. Nome do teste deve referenciar o comportamento (ex.: `Checkout_ZeroAmount_ClosesTicketWithoutAwaitingPayment`).

---

## 25. Controles obrigatÃ³rios â€” zero entrega sem testes verdes

Esta secÃ§Ã£o **elimina** a possibilidade de â€œcÃ³digo entregueâ€ sem suÃ­te automatizada passando. **Normas tÃ©cnicas** abaixo tÃªm o mesmo peso que regras de negÃ³cio.

### 25.1 DefiniÃ§Ã£o normativa de â€œentregarâ€

**Entregar** cÃ³digo significa **apenas**: merge na branch principal (`main` / `master`) apÃ³s **pull request** cujo **Ãºltimo commit** tenha passado em **todos** os trabalhos obrigatÃ³rios do ficheiro **`.github/workflows/ci.yml`** (ou equivalente migrado para outro fornecedor, mantendo os mesmos passos).

**Proibido:**

- Push **direto** em `main` / `master` (sem PR), exceto administrador de repositÃ³rio em **hotfix** documentado em ADR â€” **fora do fluxo normal**.  
- Merge de PR com **checks falhados** ou **cinzentos** (skipped por erro de configuraÃ§Ã£o).  
- Declarar tarefa â€œconcluÃ­daâ€ em documentaÃ§Ã£o de release sem **URL do run de CI** verde anexado ou hash do commit.

### 25.2 Ficheiros obrigatÃ³rios no repositÃ³rio

| Ficheiro | FunÃ§Ã£o |
|----------|--------|
| `.github/workflows/ci.yml` | Pipeline Ãºnica de verdade; **nÃ£o** duplicar lÃ³gica de verificaÃ§Ã£o sÃ³ em documentaÃ§Ã£o. |
| `.githooks/pre-commit` | Bloqueia `git commit` local se `dotnet test` falhar (quando existir `backend/*.sln`). |
| `AGENTS.md` | Regras para humanos e **agentes de IA** (incl. Cursor). |
| `.cursor/rules/tdd-entrega-zero-risco.mdc` | Regra **alwaysApply** â€” ver Â§25.4. |
| `docs/BRANCH_PROTECTION.md` | Passos para ativar **branch protection** no GitHub (nÃ£o automatizÃ¡veis por ficheiro). |

### 25.3 Hooks Git locais (obrigatÃ³rio para cada desenvolvedor)

ApÃ³s clonar:

```bash
git config core.hooksPath .githooks
```

No Windows (PowerShell):

```powershell
git config core.hooksPath .githooks
```

O hook **`pre-commit`** executa `dotnet test` na soluÃ§Ã£o em `backend/` quando **`backend/Parking.sln`** (ou qualquer `*.sln` sob `backend/`) existir. Se os testes falharem, o commit **Ã© abortado**.

**ExceÃ§Ã£o:** commit com `--no-verify` **sÃ³** com aprovaÃ§Ã£o escrita de dois maintainers em ADR â€” **proibido** no fluxo normal; mencionar na spec como **violaÃ§Ã£o** se usado sem ADR.

### 25.4 Agentes de IA (Cursor e similares)

Devem obedecer integralmente a **`AGENTS.md`** e Ã  regra **`.cursor/rules/tdd-entrega-zero-risco.mdc`**. Em particular: **nunca** afirmar â€œfeitoâ€, â€œprontoâ€, â€œconcluÃ­doâ€ sem **evidÃªncia** de comando `dotnet test` (e testes front quando aplicÃ¡vel) com **cÃ³digo de saÃ­da 0** no output da sessÃ£o, **ou** link para run de CI verde para o commit em causa.

### 25.5 Branch protection (GitHub)

ConfiguraÃ§Ã£o obrigatÃ³ria na UI do repositÃ³rio (passos em `docs/BRANCH_PROTECTION.md`):

- **Require a pull request before merging**  
- **Require status checks to pass** â€” marcar o job `ci` (ou nome definido no workflow)  
- **Do not allow bypasses** para roles nÃ£o administrativas  
- **Include administrators** (recomendado: tambÃ©m exigem CI)

### 25.6 O que ainda nÃ£o Ã© possÃ­vel garantir por ficheiro

- **Comportamento de um humano** que use `--no-verify` ou desative branch protection â€” mitigado por revisÃ£o e polÃ­tica de equipa.  
- **Flaky tests** â€” mitigado por retries limitados no CI e proibiÃ§Ã£o de testes instÃ¡veis sem issue.

---

## 26. Script de verificaÃ§Ã£o local (opcional mas recomendado)

Antes de `git push`, executar **`scripts/verify.ps1`** (Windows) ou **`scripts/verify.sh`** (Unix) â€” deve invocar os mesmos passos que o CI (build + test). Se o script falhar, **nÃ£o** abrir PR.

---

**Fim SPEC v8.7**

