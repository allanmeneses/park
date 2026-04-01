# SPEC FRONTEND v1.7 — ALINHADA AO BACKEND v8.7

Documento canônico de **UI/UX e cliente HTTP**. Implementar exatamente. Backend: `SPEC.md` v8.7.

**Idioma de interface:** português Brasil (PT-BR), strings literais nas tabelas abaixo (não parafrasear).

---

## 1. Escopo

**Incluído:** duas aplicações cliente — **Web (SPA Vue 3)** e **Android nativo** — com as mesmas rotas de negócio, mesma API e mesmos textos.

**FORA DE ESCOPO:** iOS; PWA/service worker além da fila §10; tema escuro; i18n fora PT-BR; **CRUD de pacotes** na UI (só listagem no gestor, §5.12); **desbloqueio de operador** `POST /admin/operators/{id}/unsuspend` na UI (usar API/ferramenta externa na v1 se necessário); **App Links** / URLs HTTPS abrindo o app Android.

---

## 1.1 Stack Web (fechada — sem React)

| Pacote / ferramenta | Versão mínima fixa |
|---------------------|-------------------|
| **Node.js** | 20.x LTS |
| **Vue** | **3.4.21** |
| **Vite** | **5.2.11** |
| **vue-router** | **4.3.2** |
| **pinia** | **2.1.7** |
| **typescript** | **5.4.5** |
| **axios** | **1.6.8** |
| **qrcode** (geração de imagem a partir de string EMV) | **1.5.3** |

**Arquitetura obrigatória:**

- **Composition API** + `<script setup lang="ts">` nos SFC.
- **Pinia:** store **`useAuthStore`**: `accessToken`, `refreshToken`, `expiresAtEpoch`, `scheduleRefresh()`, `clear()`; store **`useOfflineQueueStore`**: fila §10.
- **HTTP:** instância única `axios.create({ baseURL: import.meta.env.VITE_API_BASE })` + interceptors: anexar `Authorization`; em **401** uma tentativa de refresh (§3.3) antes de falhar.
- **QRCode:** em `op_pay_pix` / `cli_pay_pix` / `loj_pay_pix`, usar `import QRCode from 'qrcode'` e `await QRCode.toDataURL(props.qrPayload, { width: 280, margin: 2 })` → binding em `<img :src="dataUrl" alt="" />` (alt vazio, decorativa; botão **B9** é acessível).

**Vue Router:** modo **`createWebHistory()`** (sem hash); rotas espelham §4.4 (paths exatos).

**Variáveis Vite:** apenas **`VITE_API_BASE`** (string sem barra final). **Proibido** usar `REACT_APP_*`.

---

## 1.2 Stack Android (fechada)

| Item | Valor fixo |
|------|-------------|
| **Kotlin** | 1.9.24 |
| **Android Gradle Plugin** | 8.3.2 |
| **Gradle** | 8.4 |
| **compileSdk** / **targetSdk** | 34 |
| **minSdk** | 26 |
| **UI** | Jetpack **Compose** (BOM **2024.05.00**; sem Views/XML para telas de negócio) |
| **Navegação** | Navigation Compose |
| **Rede** | Retrofit **2.9.0** + OkHttp **4.12.0** + Moshi **1.15.1** (converter-moshi) |
| **Async** | Kotlin Coroutines **1.8.0** |
| **Armazenamento seguro** | `androidx.security:security-crypto:1.1.0-alpha06` (EncryptedSharedPreferences §3.2) |

**Módulos:** `app` único na v1 (sem multi-module obrigatório).

**QR em Compose:** usar **`remember { mutableStateOf<ImageBitmap?>(null) }`** preenchido por **`com.google.zxing`** **3.5.3** (`BarcodeEncoder` / `QRCodeWriter`) a partir da string `qr_code`, ou biblioteca **`qrcode-kotlin` 4.0.6** — **fixar:** **`com.google.zxing:core:3.5.3`** + geração de `Bitmap` convertido para `ImageBitmap`.

**Deep links externos:** **não** implementar `intent-filter` https na v1; navegação só **interna** (`NavHost`).

---

## 1.3 Monorepo e identificação Android (fechada)

**Raiz do repositório** (alinhada à §1.1 de `SPEC.md` v8.7):

| Pasta | Conteúdo |
|-------|----------|
| `backend/` | Solução .NET (`Parking.sln` e `src/*`) |
| `frontend-web/` | App Vue 3 + Vite (projeto criado nesta pasta) |
| `android/` | Projeto Android Studio (módulo `app`) |
| `database/` | `init/`, `seed/` |
| `docker-compose.yml`, `.env.example` | Infra local |

**Android — identificação fixa:**

| Campo | Valor |
|-------|--------|
| `applicationId` | `com.estacionamento.parking` |
| `namespace` (Gradle) | `com.estacionamento.parking` |
| Pacote Kotlin base | `com.estacionamento.parking` |

**BuildConfig Android:** `API_BASE` = `http://10.0.2.2:8080/api/v1` em **emulador** (host `localhost` da máquina); em **dispositivo físico na mesma LAN**, usar IP da máquina de dev (documentar em `README` do app ou `local.properties` não versionado).

---

## 2. API e ambientes

- **Prefixo:** `/api/v1` (seção 18 do backend).
- **Base URL Web:** **`VITE_API_BASE`** (string terminal **sem** barra final); requests = `{BASE}{path}`.
- **Base URL Android:** `BuildConfig.API_BASE` (release/prod separados permitidos; comportamento idêntico).
- **Headers em toda requisição autenticada:**  
  `Authorization: Bearer {access_token}`  
  `Content-Type: application/json` quando houver body JSON.
- **SUPER_ADMIN:** além do Bearer, **`X-Parking-Id: {uuid}`** em **todas** as requisições após escolha do tenant (§4.4).
- **Idempotency:** onde o backend exige: **novo UUID v4 por ação de usuário distinta** (cada tap em “Confirmar entrada” / “Confirmar checkout” gera nova chave). **Itens enfileirados offline (§10)** armazenam a chave no item da fila e **reutilizam** essa mesma chave em todo retry de sincronização daquele item.

---

## 3. Armazenamento de sessão (determinístico)

### 3.1 Web

| Chave / local | Conteúdo |
|---------------|----------|
| `sessionStorage`, key `parking.v1.access` | `access_token` |
| `localStorage`, key `parking.v1.refresh` | `refresh_token` |

Se `sessionStorage` indisponível: guardar `access_token` só em **variável em memória** (perde ao F5); `refresh_token` mantém em `localStorage`. Ao carregar app: se há refresh e não há access, chamar `POST /auth/refresh` antes da primeira rota protegida.

### 3.2 Android

- **`EncryptedSharedPreferences`**, nome do arquivo **`parking_auth_prefs`**, master key via `MasterKey` (AES256-GCM padrão AndroidX).
- Chaves: **`access_token`**, **`refresh_token`** (strings).

### 3.3 Proativa refresh

- Ao receber `expires_in` do login/refresh: agendar timer **`expires_in - 120` segundos** para chamar `POST /auth/refresh` com o refresh armazenado; sobrescrever ambos os tokens com a resposta.
- Em qualquer resposta **401** `UNAUTHORIZED` (exceto em `/auth/login`): **uma** tentativa de `POST /auth/refresh`; se falhar → limpar tokens e navegar para **Login** (§5.1).

---

## 4. Perfis, navegação e gates

### 4.1 Mapeamento role → shell

Após login, o JWT determina o **shell** inicial (substituível por rota guard):

| `role` (JWT) | Tela inicial (nome) | ID rota |
|----------------|---------------------|---------|
| OPERATOR | Operador — Início | `op_home` |
| MANAGER, ADMIN | Gestor — Painel | `mgr_dashboard` |
| CLIENT | Cliente — Carteira | `cli_wallet` |
| LOJISTA | Lojista — Carteira | `loj_wallet` |
| SUPER_ADMIN | Super — Tenant | `adm_tenant` |

### 4.2 Guard de rota

- Sem `access_token` válido em memória/storage conforme §3 → apenas `login` acessível.
- Com token: se rota não está na lista **permitida** do `role` (§6) → mostrar tela **`forbidden`** (§5.9) com HTTP nunca disparado para a API daquela feature.

### 4.3 SUPER_ADMIN — tenant ativo

- Variável em memória **`active_parking_id`** (UUID), **não** persistida em disco.
- Tela **`adm_tenant`**: campo obrigatório “ID do estacionamento (UUID)” + botão “Definir”. Ao definir: validar formato UUID v4 (regex `^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$` case insensitive); salvar em `active_parking_id` e enviar como `X-Parking-Id` em todas as chamadas até logout.
- Se usuário SUPER_ADMIN navegar para ação que chama API sem `active_parking_id` definido → snackbar/alert: texto **S15** e não enviar request.

### 4.4 Deep links (Web)

| Path | Destino após auth |
|------|-------------------|
| `/login` | `login` |
| `/operador` | `op_home` |
| `/operador/ticket/:id` | `op_ticket_detail` |
| `/gestor` | `mgr_dashboard` |
| `/gestor/movimentos` | `mgr_movements` |
| `/gestor/analises` | `mgr_analytics` |
| `/gestor/caixa` | `mgr_cash` |
| `/gestor/config` | `mgr_settings` |
| `/cliente` | `cli_wallet` |
| `/lojista` | `loj_wallet` |

Android: **apenas** navegação interna `NavHost` com **IDs de rota** semanticamente iguais aos paths Web (ex. rota `"operador/ticket/{id}"`).

---

## 5. Catálogo de telas (comportamento fechado)

Cada tela: **ID**, **quem acessa** (roles), **API**, **estados UI**, **textos**.

Estados globais de tela: `loading` | `ready` | `error` | `empty` (quando aplicável).  
**Erro:** exibir `message` do JSON se existir; senão mapa §8 para `code`.

---

### 5.1 `login`

**Roles:** não autenticado.  
**Layout:** email (type email), senha (password), botão primário **B1**.

**API:** `POST /auth/login` body `{ email, password }`.  
**Sucesso:** persistir tokens §3; navegar para shell §4.1.  
**401 + code `OPERATOR_BLOCKED`:** toast/alert **E1**, não limpar campos.  
**429 `LOGIN_THROTTLED`:** alert **E2**.

Campos vazios ao enviar: não chamar API; label campo em erro **E3** no primeiro inválido.

---

### 5.2 `op_home` — Operador início

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Conteúdo:**

1. Lista: `GET /tickets/open` em `onResume` / ao entrar na tela e ao puxar refresh (pull).  
2. Botão primário **B2** → `op_entry_plate`.  
3. Cada item: mostrar `plate`, `entry_time` (formato local **`dd/MM/yyyy HH:mm`** — timezone **do dispositivo** só para exibição), status. Tap → `op_ticket_detail` com `ticketId`.

**Menu overflow (⋮)** no topo (Android toolbar / Web header):

- Item **B3** → envia `POST /operator/problem` body `{}`; em sucesso toast **T1**; em erro toast com message.

**Estados:** `empty` com texto **S1** quando `items.length===0`.

**Offline:** se `navigator.onLine === false` (Web) ou `ConnectivityManager` sem rede (Android): banner fixo topo texto **S2**; **B2** desabilitado; lista ainda pode mostrar cache da última resposta com selo “Dados podem estar desatualizados” **S3** (obrigatório se exibir cache).

---

### 5.3 `op_entry_plate` — Nova entrada

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Campos:** texto placa, máscara visual livre, **validação final** por regex backend §6 (ambos formatos **OU**); normalizar uppercase ao perder foco.

**API:** `POST /tickets` + header Idempotency-Key.  
**201:** toast **T2**, voltar `op_home` e refresh lista.  
**400 `PLATE_INVALID`:** campo erro **E4**.  
**409 `PLATE_HAS_ACTIVE_TICKET`:** alert **E5**.

**Offline:** se offline → enfileirar operação (§12); banner **S2**.

---

### 5.4 `op_ticket_detail`

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Entrada:** `ticketId`.  
**API:** `GET /tickets/{id}` ao abrir e após ações que alteram ticket.

**Exibir:** placa, entrada, saída se houver, status.

**Ações:**

- Se `ticket.status === 'OPEN'`: botão **B4** → `op_checkout`.  
- Se `ticket.status === 'AWAITING_PAYMENT'`: botão **B5** → `op_pay_method` com `paymentId = payment.id` (do DTO).  
- Se `ticket.status === 'CLOSED'`: só leitura; texto **S4**.

---

### 5.5 `op_checkout`

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `POST /tickets/{id}/checkout` com Idempotency-Key; body `{}` (não enviar `exit_time` a menos que relógio confiável; **padrão servidor**).

**Resposta:**

- Se `amount === "0.00"` ou `Number(amount)===0`: alert sucesso **T3** (incluir breakdown horas se quiser — opcional); voltar `op_home`.  
 Se `amount > 0`: navegar `op_pay_method` com `paymentId` da resposta (obrigatório).

**409 `INVALID_TICKET_STATE`:** alert **E6**, voltar `op_ticket_detail`.

---

### 5.6 `op_pay_method` — Escolha pagamento (ticket)

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Props:** `paymentId`, opcionalmente `ticketId` para voltar.

**Pré-check caixa:** `GET /cash` silencioso ao abrir.

**Botões:**

1. **B6 (PIX)** — sempre habilitado online.  
2. **B7 (Cartão)** — stub: habilitado online.  
3. **B8 (Dinheiro)** — habilitado **somente** se `GET /cash` retornou `open != null`; senão botão visível **desabilitado** com tooltip/subtítulo **S5**.

**Offline:** **B6 e B7 desabilitados**; **B8** permitido **somente** se já havia sessão de caixa aberta antes de perder rede (estado local `cash_open_cached===true` vindo da última `GET /cash` bem-sucedida?) — **fechar regra:** se offline, **todos** métodos desabilitados e alert **S6** (dinheiro também exige API no spec backend para POST cash — logo **offline: nenhum pagamento**). Texto **S6** obrigatório.

Tap **B6** → `op_pay_pix` com `paymentId`.  
Tap **B7** → `op_pay_card` com `paymentId`.  
Tap **B8** → confirmar diálogo **D1**; se OK → `POST /payments/cash` body `{ payment_id }`; sucesso **T4** → `op_home`.

---

### 5.7 `op_pay_pix`

**Props:** `paymentId`.

**API:** `POST /payments/pix` body `{ payment_id }`.

**UI:** exibir QR: gerar imagem a partir da string **`qr_code`** (EMV/payload como texto — usar lib QR no Web/Android). Botão secundário **B9** copiar `qr_code` para clipboard; toast **T5** ao copiar.

**Contador:** `expires_at` − `Date.now()` a cada 1s UI; ao ≤ 0: texto **S7** e botão **B10 “Gerar novo QR”** que **rechama** `POST /payments/pix` (mesmo `payment_id`).

**Polling:** a cada **2000 ms** chamar `GET /payments/{paymentId}` até:

- `status === 'PAID'` → toast **T4** → `op_home`, **ou**
- `status === 'EXPIRED'` → parar polling, mostrar **S7** e **B10**, **ou**
- `status === 'FAILED'` → alert **E7** → `op_pay_method`.

**Parada de segurança:** após **900000 ms** (15 min) desde `onMount` da tela, parar polling e mostrar **S8** + botão **B10**.

**Android/Web:** ao sair da tela (`onDispose`/`useEffect cleanup`), **cancelar** timer de polling.

---

### 5.8 `op_pay_card`

**Props:** `paymentId`.

**Mostrar** valor: obter via último `GET /payments/{id}` ou estado passado (obrigatório ter amount antes de confirmar).

**API:** `POST /payments/card` `{ payment_id, amount }` com `amount` **igual** ao string do DTO (normalizar para número decimal com 2 casas no JSON).

**Sucesso:** **T4** → `op_home`.  
**409 `AMOUNT_MISMATCH`:** **E8**.

---

### 5.9 `forbidden`

**Roles:** qualquer autenticado que caiu em rota não permitida.  
**Conteúdo:** ícone bloqueio, título **S9**, subtítulo **S10**, botão **B11** → shell inicial do role.

---

### 5.10 `mgr_dashboard`

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /dashboard`.

**Cards (ordem fixa):**

1. Faturamento (hoje): valor `faturamento` formatado **R$ #,##0.00** `pt-BR`.  
2. Ocupação: `ocupacao` como **percentual** `0.0%`–`100.0%` com uma casa decimal.  
3. Check-outs hoje: `tickets_dia` inteiro.  
4. Uso convênio: se `uso_convenio === null` exibir **“—”**; senão percentual **`(uso_convenio*100).toFixed(1)%`**.

**Navegação:** botões **B22** → `mgr_movements`, **B23** → `mgr_analytics`, **B12** → `mgr_cash`, **B13** → `mgr_settings`.

**Paridade Web/Android:** no painel (`mgr_dashboard`), **B22** e **B23** devem existir **nos dois** clientes, na mesma ordem acima (ADMIN e MANAGER têm o mesmo conjunto).

---

### 5.10.1 `mgr_movements` — Extrato com filtros

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /manager/movements`.

**Filtros obrigatórios na UI:**

- janela rápida (**24h**, **7d**, **30d**),
- intervalo manual (`from`/`to`, UTC),
- tipo (`TICKET_PAYMENT`, `PACKAGE_PAYMENT`, `LOJISTA_USAGE`, `CLIENT_USAGE`).

**Exibir:**

- resumo (`total_ticket`, `total_package`, `usages_lojista`, `usages_client`, `count`);
- lista paginada/simples com `at`, `kind`, `amount`, `method`.

Botão **B23** deve abrir `mgr_analytics`.

---

### 5.10.2 `mgr_analytics` — Tendências e horários de pico

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /manager/analytics?days=N` (1..90).

**Exibir:**

- totais do período (`revenue`, `payments`, `checkouts`),
- `trend_by_day`,
- `gains_by_hour`,
- `peak_hours` (top horários).

---

### 5.11 `mgr_cash`

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**Ao abrir:** `GET /cash`.

- Se `open === null`: botão **B14** `POST /cash/open` → sucesso refresh; **B15** fechar **desabilitado**.  
- Se `open !== null`: exibir `expected_amount` formatado BRL; **B15** habilitado → formulário `actual_amount` (decimal) → `POST /cash/close` `{ session_id: open.session_id, actual_amount }`.  
 Se resposta `alert === true`: após fechar modal sucesso, mostrar alert não bloqueante **T6** (divergência).

**Textos:** título **S11**.

---

### 5.12 `mgr_settings`

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /settings` ao abrir; `POST /settings` ao salvar.

**Campos:** `price_per_hour`, `capacity` (inteiro > 0). Validação cliente: capacity ≥ 1; price ≥ 0.01.

**Salvar:** toast **T7** ou erro campo.

**Lista de pacotes (somente leitura nesta versão):** `GET /recharge-packages?scope=CLIENT` e outra chamada `scope=LOJISTA`; exibir duas listas com **horas** e **preço** — texto **S12** se vazio. *(Cadastro/edição de pacotes: FORA DE ESCOPO frontend; dados vêm do seed/DB.)*

---

### 5.13 `cli_wallet`

**Roles:** CLIENT.

**API:** `GET /client/wallet`.

**Exibir:** saldo horas inteiro, expiração se não null (data **dd/MM/yyyy**).

**B16** → `cli_buy`.  
**B17** → `cli_history`.

---

### 5.14 `cli_history`

**Roles:** CLIENT.

**API:** `GET /client/history?limit=50` + `cursor` se `next_cursor` anterior.

**Lista:** cada item: `kind` **“Compra”** se PURCHASE / **“Uso”** se USAGE; `delta_hours` com sinal **+** para compra; data formatada.

**Infinite scroll:** ao fim da lista, se `next_cursor` não null, próxima página.

---

### 5.15 `cli_buy`

**Roles:** CLIENT.

**API:** `GET /recharge-packages?scope=CLIENT`.

**Lista** pacotes: botão por item **B18** abre escolha: **Crédito** / **PIX** (modal dois botões).

- **Crédito:** confirma **D2** → `POST /client/buy` `{ package_id, settlement: "CREDIT" }` + Idempotency → sucesso **T8** → refresh wallet em background, voltar `cli_wallet`.  
- **PIX:** `POST /client/buy` `{ package_id, settlement: "PIX" }` + Idempotency → recebe `payment_id` → navegar `cli_pay_pix` com esse id.

---

### 5.16 `cli_pay_pix`

**Roles:** CLIENT.

Igual `op_pay_pix` (§5.7) com mesmas regras de polling e QR; sucesso **T8** → `cli_wallet`.

---

### 5.17 `loj_wallet`, `loj_history`, `loj_buy`, `loj_pay_pix`

**Roles:** LOJISTA.  
**Comportamento:** idêntico a cliente substituindo:

- endpoints `/lojista/*`,  
- `GET /recharge-packages?scope=LOJISTA`,  
- settlement e labels **S13** onde falar “cliente”.

---

### 5.18 `adm_tenant` — SUPER_ADMIN

**Proibido para ADMIN / MANAGER / OPERATOR:** esta rota existe **apenas** para **SUPER_ADMIN** (matriz §6). O **administrador do tenant** (**ADMIN**) inicia em **gestão** (`mgr_dashboard`) com o `parking_id` do login — não escolhe estacionamento global nem cria tenant.

**Conteúdo (Web e Android):**

1. **Criar estacionamento:** formulário com e-mail e senha do **administrador do tenant** (ADMIN) e e-mail e senha do **primeiro operador** — contas **distintas**. Chamada `POST /admin/tenants` (sem `X-Parking-Id` necessário para este POST). Sucesso: mensagem clara; atualizar lista.
2. **Lista:** `GET /admin/tenants`; permitir escolher um item para definir `active_parking_id` / `X-Parking-Id`.
3. **UUID manual (avançado):** campo UUID + “Continuar” (§4.3); após válido: **B20** → `mgr_dashboard`, **B21** → `op_home`. Sem tenant ativo: **S15** ao tocar em Gestão/Operação.

---

## 6. Matriz rota de UI × role

| ID rota | OP | MG | AD | CL | LJ | SP |
|---------|:--:|:--:|:--:|:--:|:--:|:--:|
| login | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| op_home | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| op_entry_plate | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| op_ticket_detail | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| op_checkout | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| op_pay_method | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| op_pay_pix | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| op_pay_card | ✓ | ✓ | ✓ | ✗ | ✗ | ✓* |
| mgr_dashboard | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| mgr_movements | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| mgr_analytics | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| mgr_cash | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| mgr_settings | ✗ | ✓ | ✓ | ✗ | ✗ | ✓* |
| cli_* | ✗ | ✗ | ✗ | ✓ | ✗ | ✗ |
| loj_* | ✗ | ✗ | ✗ | ✗ | ✓ | ✗ |
| adm_tenant | ✗ | ✗ | ✗ | ✗ | ✗ | ✓ |
| forbidden | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

\*Requer `active_parking_id` para chamadas API; ver §4.3.

**MANAGER** e **ADMIN** podem usar **tanto** fluxo operador **quanto** gestor: **dois ícones** na navegação principal **TabBar** (Android) ou **sidebar** (Web): “Operação” (→ `op_home`) e “Gestão” (→ `mgr_dashboard`). **OPERATOR** só “Operação”. **SUPER_ADMIN**: após definir tenant, mesmo padrão que MANAGER ou só gestão — **fixar:** SUPER_ADMIN vê **Operação + Gestão** como MANAGER.

---

## 7. Design tokens (fixos)

| Token | Valor | Uso |
|-------|-------|-----|
| `color.primary` | `#1565C0` | Botões primários, links |
| `color.error` | `#C62828` | Erros, alertas críticos |
| `color.surface` | `#FFFFFF` | Fundo telas |
| `color.text` | `#212121` | Texto principal |
| `color.text_secondary` | `#757575` | Subtítulos |
| `space.page` | `16dp` / `16px` | Padding horizontal listas e formulários |
| `touch.min` | `48dp` | Altura mínima alvo toque |
| `font.title` | 20sp / 1.25rem semibold | Título de tela |
| `font.body` | 16sp / 1rem regular | Corpo |

**Componente primário:** retângulo preenchido `primary`, texto branco `#FFFFFF`.

---

## 8. Mapa de códigos HTTP → mensagem UX (fallback)

Se `message` vier vazio no JSON:

| code | Texto |
|------|--------|
| VALIDATION_ERROR | Verifique os dados informados. |
| UNAUTHORIZED | Sessão expirada. Faça login novamente. |
| FORBIDDEN | Você não tem permissão. |
| NOT_FOUND | Registro não encontrado. |
| CONFLICT | Operação não permitida no estado atual. |
| PLATE_INVALID | Placa inválida. |
| PLATE_HAS_ACTIVE_TICKET | Já existe ticket aberto para esta placa. |
| INVALID_TICKET_STATE | Ticket não está nesta etapa. |
| LOJISTA_WALLET_MISSING | Convênio indisponível: carteira do lojista não configurada. |
| PAYMENT_ALREADY_PAID | Pagamento já confirmado. |
| AMOUNT_MISMATCH | Valor não confere. |
| CASH_SESSION_REQUIRED | Abra o caixa antes de receber em dinheiro. |
| OPERATOR_BLOCKED | *(ver E1)* |
| TENANT_UNAVAILABLE | Estacionamento indisponível. Tente mais tarde. |
| LOGIN_THROTTED | Muitas tentativas. Aguarde e tente novamente. |
| CLOCK_SKEW | Relógio do aparelho incorreto. Ajuste a data/hora. |
| INTERNAL | Erro no servidor. Tente novamente. |

---

## 9. Tabela de strings (literais)

| ID | Texto |
|----|--------|
| B1 | Entrar |
| B2 | Nova entrada |
| B3 | Registrar problema |
| B4 | Registrar saída (checkout) |
| B5 | Pagar |
| B6 | PIX |
| B7 | Cartão |
| B8 | Dinheiro |
| B9 | Copiar código PIX |
| B10 | Gerar novo QR |
| B11 | Voltar ao início |
| B12 | Caixa |
| B13 | Configurações |
| B14 | Abrir caixa |
| B15 | Fechar caixa |
| B16 | Comprar horas |
| B17 | Histórico |
| B18 | Selecionar |
| B20 | Gestão |
| B21 | Operação |
| B22 | Insights |
| B23 | Análises |
| D1 | Confirmar recebimento em dinheiro neste valor? |
| D2 | Confirmar compra a crédito interno? O valor será registrado. |
| S1 | Nenhum veículo no pátio. |
| S2 | Sem conexão. Algumas ações ficam bloqueadas. |
| S3 | Dados podem estar desatualizados (offline). |
| S4 | Ticket encerrado. |
| S5 | Abra o caixa para habilitar dinheiro. |
| S6 | Pagamento online indisponível offline. Reconecte-se. |
| S7 | QR expirado. |
| S8 | Tempo limite de espera do pagamento. Use “Gerar novo QR”. |
| S9 | Acesso negado |
| S10 | Você não pode abrir esta área com seu perfil. |
| S11 | Sessão de caixa |
| S12 | Nenhum pacote cadastrado para este tipo. |
| S13 | (Lojista) Mesmas ações que cliente, textos com “sua carteira de convênio”. |
| S15 | Informe o ID do estacionamento (UUID) para continuar. |
| T1 | Problema registrado. |
| T2 | Entrada registrada. |
| T3 | Saída registrada. Nada a pagar. |
| T4 | Pagamento confirmado. |
| T5 | Código copiado. |
| T6 | Alerta: divergência no caixa acima do limite. |
| T7 | Configurações salvas. |
| T8 | Compra concluída. |
| E1 | Operador bloqueado. Procure o gestor. |
| E2 | Aguarde antes de tentar de novo. |
| E3 | Preencha este campo. |
| E4 | Formato de placa inválido. |
| E5 | Já existe ticket em aberto para esta placa. |
| E6 | Não foi possível registrar a saída neste estado. |
| E7 | Pagamento falhou. Escolha outro método ou tente novamente. |
| E8 | Valor enviado não confere com o ticket. |

---

## 10. Offline — fila (espelho §16 backend)

**Operações enfileiráveis:**

- `POST /tickets`
- `POST /tickets/{id}/checkout`

**Estrutura item da fila:**  
`{ id_local: uuid, method: "POST", path: string, headers: { Idempotency-Key, Authorization }, body: object|null, created_at_epoch: number }`

**Drain:** ao voltar `online`, enviar **FIFO**; **máx. 5** tentativas por item com backoff **1s, 2s, 4s, 8s, 16s** entre tentativas daquele item. Após falha final: notificar **T9** “Fila: operação não enviada” + manter item para revisão manual **FORA DE ESCOPO** UI detalhada — **mínimo:** toast **T9**.

**T9:** Não foi possível sincronizar uma operação. Verifique na lista de tickets.

**Proibido** enfileirar `POST /payments/*`.

---

## 11. Acessibilidade (mínimo)

- Contraste texto/fundo ≥ **4.5:1** para `text` sobre `surface` (tokens §7 atendem material padrão).  
- **Web (Vue):** cada controle clicável `B*` com atributo **`aria-label`** igual ao texto do botão (literais §9).  
- **Android (Compose):** `Modifier.semantics { contentDescription = "..." }` com o mesmo texto **B\***.  
- Campo placa: **Web:** `aria-label="Placa do veículo"`; **Android:** `label = { Text("Placa do veículo") }` no `OutlinedTextField`.

---

## 12. Referência cruzada backend

| Necessidade frontend | Onde no `SPEC.md` v8.7 |
|----------------------|-------------------------|
| Placas | §6 |
| Stack servidor / repo | §1.1 |
| TDD / CI / DoD / zero risco | §23–§26, `AGENTS.md` |
| RBAC API | §17 |
| DTO pagamento / polling | §18 `GET /payments/{id}` |
| Pacotes lista | §18 `GET /recharge-packages` |
| Settings leitura | §18 `GET /settings` |
| Docker / `.env` | §19 + `README.md` |

---

## 13. Qualidade, TDD e definição de pronto (front)

Normativo em conjunto com **`SPEC.md` §23**. O utilizador **não** valida manualmente cada entrega; **aceite** = suíte automatizada verde + DoD abaixo.

### 13.1 Princípios

- **TDD** onde aplicável: teste de componente/composable **falha** → implementação mínima → refatora.  
- **Nenhum merge** na branch principal com testes falhando ou ignorados sem justificativa (issue + prazo).

### 13.2 Web (Vue 3)

| Tipo | Ferramenta mínima fixa | Escopo |
|------|------------------------|--------|
| **Unit** | **Vitest 2.x** (última minor estável) + **@vue/test-utils** | Composables (`useApi`, stores Pinia), helpers de validação de placa (regex §6), parsers de erro |
| **E2E** | **Playwright** (última 1.x estável) | Navegador headless Chromium; baseURL `http://localhost:5173`; API real em `VITE_API_BASE` (subir backend + Postgres antes do job) |

**Cenários E2E obrigatórios (mínimo):**

1. Login (credenciais de fixture em `.env.test` ou seed) → redireciona para shell do role.  
2. **OPERATOR:** lista tickets abertos ou vazia → **Nova entrada** com placa válida → aparece na lista.  
3. **Fluxo pagamento PIX (Stub):** checkout com valor > 0 → tela PIX → **simular** webhook via **API** (curl script chamado no `beforeAll` ou helper HTTP no teste) ou fixture que chama backend — assert ticket encerrado ao consultar API ou UI atualizada após polling.

**Cobertura:** **≥ 60%** de linhas em `src/` excluindo `main.ts` e assets; falha de CI se abaixo (Coverlet/V8 coverage no Vitest).

### 13.3 Android (Compose)

| Tipo | Ferramenta | Escopo |
|------|------------|--------|
| **Unit** | JUnit 4 + coroutines test | ViewModels, mapeadores DTO |
| **UI** | **Compose UI Test** (debug) | Pelo menos **um** fluxo: login fake (mock servidor com **MockWebServer** OkHttp OU contra API de teste) + tela inicial |

**E2E instrumentado completo** (Firebase Test Lab / dispositivo) — **recomendado** na v1; se não implementado na primeira entrega, **obrigatório** documentar em `README` com data alvo e manter **Compose UI Test** cobrindo telas críticas.

### 13.4 DoD — front (incremento)

1. `npm run test` (Vitest) e `npm run test:e2e` (Playwright) **verdes** no CI.  
2. Android: `./gradlew test` e `./gradlew connectedDebugAndroidTest` **verdes** quando aplicável ao ambiente CI.  
3. Nenhum `console.error` não tratado em build de produção (ESLint `no-console` em `warn` ou equivalente).

### 13.5 CI front (obrigatório)

Pipeline separado ou jobs na mesma pipeline do backend:

- `frontend-web`: `npm ci`, `npm run build`, `npm run test`, `npx playwright install --with-deps`, `npm run test:e2e` (com serviços `docker compose up` + API em background).  
- `android`: `./gradlew assembleDebug test` (e connected se runner disponível).

**Merge bloqueado** se falhar.

### 13.6 Controles de repositório (anti entrega sem testes)

Obrigatório seguir **`SPEC.md` §25** e ficheiros **`AGENTS.md`**, **`.github/workflows/ci.yml`**, **`.githooks/pre-commit`**, **`.cursor/rules/tdd-entrega-zero-risco.mdc`**. Não há “entrega” sem CI verde e evidência de testes conforme essas normas.

---

**Fim SPEC FRONTEND v1.5**
