# SPEC FRONTEND v1.7 â€” ALINHADA AO BACKEND v8.7

Documento canÃ´nico de **UI/UX e cliente HTTP**. Implementar exatamente. Backend: `SPEC.md` v8.7.

**Idioma de interface:** portuguÃªs Brasil (PT-BR), strings literais nas tabelas abaixo (nÃ£o parafrasear).

---

## 1. Escopo

**IncluÃ­do:** duas aplicaÃ§Ãµes cliente â€” **Web (SPA Vue 3)** e **Android nativo** â€” com as mesmas rotas de negÃ³cio, mesma API e mesmos textos.

**FORA DE ESCOPO:** iOS; PWA/service worker alÃ©m da fila Â§10; tema escuro; i18n fora PT-BR; **CRUD de pacotes** na UI (sÃ³ listagem no gestor, Â§5.12); **desbloqueio de operador** `POST /admin/operators/{id}/unsuspend` na UI (usar API/ferramenta externa na v1 se necessÃ¡rio); **App Links** / URLs HTTPS abrindo o app Android.

---

## 1.1 Stack Web (fechada â€” sem React)

| Pacote / ferramenta | VersÃ£o mÃ­nima fixa |
|---------------------|-------------------|
| **Node.js** | 20.x LTS |
| **Vue** | **3.4.21** |
| **Vite** | **5.2.11** |
| **vue-router** | **4.3.2** |
| **pinia** | **2.1.7** |
| **typescript** | **5.4.5** |
| **axios** | **1.6.8** |
| **qrcode** (geraÃ§Ã£o de imagem a partir de string EMV) | **1.5.3** |

**Arquitetura obrigatÃ³ria:**

- **Composition API** + `<script setup lang="ts">` nos SFC.
- **Pinia:** store **`useAuthStore`**: `accessToken`, `refreshToken`, `expiresAtEpoch`, `scheduleRefresh()`, `clear()`; store **`useOfflineQueueStore`**: fila Â§10.
- **HTTP:** instÃ¢ncia Ãºnica `axios.create({ baseURL: import.meta.env.VITE_API_BASE })` + interceptors: anexar `Authorization`; em **401** uma tentativa de refresh (Â§3.3) antes de falhar.
- **QRCode:** em `op_pay_pix` / `cli_pay_pix` / `loj_pay_pix`, usar `import QRCode from 'qrcode'` e `await QRCode.toDataURL(props.qrPayload, { width: 280, margin: 2 })` â†’ binding em `<img :src="dataUrl" alt="" />` (alt vazio, decorativa; botÃ£o **B9** Ã© acessÃ­vel).

**Vue Router:** modo **`createWebHistory()`** (sem hash); rotas espelham Â§4.4 (paths exatos).

**VariÃ¡veis Vite:** apenas **`VITE_API_BASE`** (string sem barra final). **Proibido** usar `REACT_APP_*`.

---

## 1.2 Stack Android (fechada)

| Item | Valor fixo |
|------|-------------|
| **Kotlin** | 1.9.24 |
| **Android Gradle Plugin** | 8.3.2 |
| **Gradle** | 8.4 |
| **compileSdk** / **targetSdk** | 34 |
| **minSdk** | 26 |
| **UI** | Jetpack **Compose** (BOM **2024.05.00**; sem Views/XML para telas de negÃ³cio) |
| **NavegaÃ§Ã£o** | Navigation Compose |
| **Rede** | Retrofit **2.9.0** + OkHttp **4.12.0** + Moshi **1.15.1** (converter-moshi) |
| **Async** | Kotlin Coroutines **1.8.0** |
| **Armazenamento seguro** | `androidx.security:security-crypto:1.1.0-alpha06` (EncryptedSharedPreferences Â§3.2) |

**MÃ³dulos:** `app` Ãºnico na v1 (sem multi-module obrigatÃ³rio).

**QR em Compose:** usar **`remember { mutableStateOf<ImageBitmap?>(null) }`** preenchido por **`com.google.zxing`** **3.5.3** (`BarcodeEncoder` / `QRCodeWriter`) a partir da string `qr_code`, ou biblioteca **`qrcode-kotlin` 4.0.6** â€” **fixar:** **`com.google.zxing:core:3.5.3`** + geraÃ§Ã£o de `Bitmap` convertido para `ImageBitmap`.

**Deep links externos:** **nÃ£o** implementar `intent-filter` https na v1; navegaÃ§Ã£o sÃ³ **interna** (`NavHost`).

---

## 1.3 Monorepo e identificaÃ§Ã£o Android (fechada)

**Raiz do repositÃ³rio** (alinhada Ã  Â§1.1 de `SPEC.md` v8.7):

| Pasta | ConteÃºdo |
|-------|----------|
| `backend/` | SoluÃ§Ã£o .NET (`Parking.sln` e `src/*`) |
| `frontend-web/` | App Vue 3 + Vite (projeto criado nesta pasta) |
| `android/` | Projeto Android Studio (mÃ³dulo `app`) |
| `database/` | `init/`, `seed/` |
| `docker-compose.yml`, `.env.example` | Infra local |

**Android â€” identificaÃ§Ã£o fixa:**

| Campo | Valor |
|-------|--------|
| `applicationId` | `com.estacionamento.parking` |
| `namespace` (Gradle) | `com.estacionamento.parking` |
| Pacote Kotlin base | `com.estacionamento.parking` |

**BuildConfig Android:** `API_BASE` = `http://10.0.2.2:8080/api/v1` em **emulador** (host `localhost` da mÃ¡quina); em **dispositivo fÃ­sico na mesma LAN**, usar IP da mÃ¡quina de dev (documentar em `README` do app ou `local.properties` nÃ£o versionado).

---

## 2. API e ambientes

- **Prefixo:** `/api/v1` (seÃ§Ã£o 18 do backend).
- **Base URL Web:** **`VITE_API_BASE`** (string terminal **sem** barra final); requests = `{BASE}{path}`.
- **Base URL Android:** `BuildConfig.API_BASE` (release/prod separados permitidos; comportamento idÃªntico).
- **Headers em toda requisiÃ§Ã£o autenticada:**  
  `Authorization: Bearer {access_token}`  
  `Content-Type: application/json` quando houver body JSON.
- **SUPER_ADMIN:** alÃ©m do Bearer, **`X-Parking-Id: {uuid}`** em **todas** as requisiÃ§Ãµes apÃ³s escolha do tenant (Â§4.4).
- **Idempotency:** onde o backend exige: **novo UUID v4 por aÃ§Ã£o de usuÃ¡rio distinta** (cada tap em â€œConfirmar entradaâ€ / â€œConfirmar checkoutâ€ gera nova chave). **Itens enfileirados offline (Â§10)** armazenam a chave no item da fila e **reutilizam** essa mesma chave em todo retry de sincronizaÃ§Ã£o daquele item.

---

## 3. Armazenamento de sessÃ£o (determinÃ­stico)

### 3.1 Web

| Chave / local | ConteÃºdo |
|---------------|----------|
| `sessionStorage`, key `parking.v1.access` | `access_token` |
| `localStorage`, key `parking.v1.refresh` | `refresh_token` |

Se `sessionStorage` indisponÃ­vel: guardar `access_token` sÃ³ em **variÃ¡vel em memÃ³ria** (perde ao F5); `refresh_token` mantÃ©m em `localStorage`. Ao carregar app: se hÃ¡ refresh e nÃ£o hÃ¡ access, chamar `POST /auth/refresh` antes da primeira rota protegida.

### 3.2 Android

- **`EncryptedSharedPreferences`**, nome do arquivo **`parking_auth_prefs`**, master key via `MasterKey` (AES256-GCM padrÃ£o AndroidX).
- Chaves: **`access_token`**, **`refresh_token`** (strings).

### 3.3 Proativa refresh

- Ao receber `expires_in` do login/refresh: agendar timer **`expires_in - 120` segundos** para chamar `POST /auth/refresh` com o refresh armazenado; sobrescrever ambos os tokens com a resposta.
- Em qualquer resposta **401** `UNAUTHORIZED` (exceto em `/auth/login`): **uma** tentativa de `POST /auth/refresh`; se falhar â†’ limpar tokens e navegar para **Login** (Â§5.1).

---

## 4. Perfis, navegaÃ§Ã£o e gates

### 4.1 Mapeamento role â†’ shell

ApÃ³s login, o JWT determina o **shell** inicial (substituÃ­vel por rota guard):

| `role` (JWT) | Tela inicial (nome) | ID rota |
|----------------|---------------------|---------|
| OPERATOR | Operador â€” InÃ­cio | `op_home` |
| MANAGER, ADMIN | Gestor â€” Painel | `mgr_dashboard` |
| CLIENT | Cliente â€” Carteira | `cli_wallet` |
| LOJISTA | Lojista â€” Carteira | `loj_wallet` |
| SUPER_ADMIN | Super â€” Tenant | `adm_tenant` |

### 4.2 Guard de rota

- Sem `access_token` vÃ¡lido em memÃ³ria/storage conforme Â§3 â†’ apenas `login` acessÃ­vel.
- Com token: se rota nÃ£o estÃ¡ na lista **permitida** do `role` (Â§6) â†’ mostrar tela **`forbidden`** (Â§5.9) com HTTP nunca disparado para a API daquela feature.

### 4.3 SUPER_ADMIN â€” tenant ativo

- VariÃ¡vel em memÃ³ria **`active_parking_id`** (UUID), **nÃ£o** persistida em disco.
- Tela **`adm_tenant`**: campo obrigatÃ³rio â€œID do estacionamento (UUID)â€ + botÃ£o â€œDefinirâ€. Ao definir: validar formato UUID v4 (regex `^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$` case insensitive); salvar em `active_parking_id` e enviar como `X-Parking-Id` em todas as chamadas atÃ© logout.
- Se usuÃ¡rio SUPER_ADMIN navegar para aÃ§Ã£o que chama API sem `active_parking_id` definido â†’ snackbar/alert: texto **S15** e nÃ£o enviar request.

### 4.4 Deep links (Web)

| Path | Destino apÃ³s auth |
|------|-------------------|
| `/login` | `login` |
| `/cadastro/lojista` | `loj_register` |
| `/operador` | `op_home` |
| `/operador/ticket/:id` | `op_ticket_detail` |
| `/gestor` | `mgr_dashboard` |
| `/gestor/movimentos` | `mgr_movements` |
| `/gestor/analises` | `mgr_analytics` |
| `/gestor/caixa` | `mgr_cash` |
| `/gestor/lojista-convites` | `mgr_lojista_invites` |
| `/gestor/config` | `mgr_settings` |
| `/cliente` | `cli_wallet` |
| `/lojista` | `loj_wallet` |
| `/lojista/bonificar` | `loj_grant` |
| `/lojista/bonificacoes` | `loj_grant_history` |

Android: **apenas** navegaÃ§Ã£o interna `NavHost` com **IDs de rota** semanticamente iguais aos paths Web (ex. rota `"operador/ticket/{id}"`). Rota pÃºblica de cadastro lojista: Web `/cadastro/lojista`, Android `loj_register` (antes do login).

---

## 5. CatÃ¡logo de telas (comportamento fechado)

Cada tela: **ID**, **quem acessa** (roles), **API**, **estados UI**, **textos**.

Estados globais de tela: `loading` | `ready` | `error` | `empty` (quando aplicÃ¡vel).  
**Erro:** exibir `message` do JSON se existir; senÃ£o mapa Â§8 para `code`.

---

### 5.1 `login`

**Roles:** nÃ£o autenticado.  
**Layout:** email (type email), senha (password), botÃ£o primÃ¡rio **B1**.

**API:** `POST /auth/login` body `{ email, password }`.  
**Sucesso:** persistir tokens Â§3; navegar para shell Â§4.1.  
**401 + code `OPERATOR_BLOCKED`:** toast/alert **E1**, nÃ£o limpar campos.  
**429 `LOGIN_THROTTLED`:** alert **E2**.

Campos vazios ao enviar: nÃ£o chamar API; label campo em erro **E3** no primeiro invÃ¡lido.

Link para **`loj_register`** (texto **B25**).

---

### 5.1.1 `loj_register` â€” Cadastro pÃºblico lojista

**Roles:** nÃ£o autenticado.  
**Layout:** campos: cÃ³digo do lojista (10 caracteres, uppercase ao validar), cÃ³digo de ativaÃ§Ã£o, nome da loja, e-mail, senha; botÃ£o primÃ¡rio **B24**; link/voltar ao **login**.

**API:** `POST /auth/register-lojista` body `{ merchantCode, activationCode, name, email, password }` (JSON camelCase).  
**Sucesso:** persistir tokens como no login; navegar para **`loj_wallet`**.  
**400 `LOJISTA_INVITE_INVALID`**, **409 `LOJISTA_INVITE_CONSUMED`**, **409 `CONFLICT`:** exibir `message` ou mapa Â§8.

CÃ³digo do lojista com comprimento â‰  10: nÃ£o chamar API; **E9**.

---

### 5.2 `op_home` â€” Operador inÃ­cio

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**ConteÃºdo:**

1. Lista: `GET /tickets/open` em `onResume` / ao entrar na tela e ao puxar refresh (pull).  
2. BotÃ£o primÃ¡rio **B2** â†’ `op_entry_plate`.  
3. Cada item: mostrar `plate`, `entry_time` (formato local **`dd/MM/yyyy HH:mm`** â€” timezone **do dispositivo** sÃ³ para exibiÃ§Ã£o), status. Tap â†’ `op_ticket_detail` com `ticketId`.

**Menu overflow (â‹®)** no topo (Android toolbar / Web header):

- Item **B3** â†’ envia `POST /operator/problem` body `{}`; em sucesso toast **T1**; em erro toast com message.

**Estados:** `empty` com texto **S1** quando `items.length===0`.

**Offline:** se `navigator.onLine === false` (Web) ou `ConnectivityManager` sem rede (Android): banner fixo topo texto **S2**; **B2** desabilitado; lista ainda pode mostrar cache da Ãºltima resposta com selo â€œDados podem estar desatualizadosâ€ **S3** (obrigatÃ³rio se exibir cache).

---

### 5.3 `op_entry_plate` â€” Nova entrada

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Campos:** texto placa, mÃ¡scara visual livre, **validaÃ§Ã£o final** por regex backend Â§6 (ambos formatos **OU**); normalizar uppercase ao perder foco.

**API:** `POST /tickets` + header Idempotency-Key.  
**201:** toast **T2**, voltar `op_home` e refresh lista.  
**400 `PLATE_INVALID`:** campo erro **E4**.  
**409 `PLATE_HAS_ACTIVE_TICKET`:** alert **E5**.

**Offline:** se offline â†’ enfileirar operaÃ§Ã£o (Â§12); banner **S2**.

---

### 5.4 `op_ticket_detail`

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Entrada:** `ticketId`.  
**API:** `GET /tickets/{id}` ao abrir e apÃ³s aÃ§Ãµes que alteram ticket.

**Exibir:** placa, entrada, saÃ­da se houver, status.

**AÃ§Ãµes:**

- Se `ticket.status === 'OPEN'`: botÃ£o **B4** â†’ `op_checkout`.  
- Se `ticket.status === 'AWAITING_PAYMENT'`: botÃ£o **B5** â†’ `op_pay_method` com `paymentId = payment.id` (do DTO).  
- Se `ticket.status === 'CLOSED'`: sÃ³ leitura; texto **S4**.

---

### 5.5 `op_checkout`

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `POST /tickets/{id}/checkout` com Idempotency-Key; body `{}` (nÃ£o enviar `exit_time` a menos que relÃ³gio confiÃ¡vel; **padrÃ£o servidor**).

**Resposta:**

- Se `amount === "0.00"` ou `Number(amount)===0`: alert sucesso **T3** (incluir breakdown horas se quiser â€” opcional); voltar `op_home`.  
 Se `amount > 0`: navegar `op_pay_method` com `paymentId` da resposta (obrigatÃ³rio).

**409 `INVALID_TICKET_STATE`:** alert **E6**, voltar `op_ticket_detail`.

---

### 5.6 `op_pay_method` â€” Escolha pagamento (ticket)

**Roles:** OPERATOR, MANAGER, ADMIN, SUPER_ADMIN\*.

**Props:** `paymentId`, opcionalmente `ticketId` para voltar.

**PrÃ©-check caixa:** `GET /cash` silencioso ao abrir.

**BotÃµes:**

1. **B6 (PIX)** â€” sempre habilitado online.  
2. **B7 (CartÃ£o)** â€” stub: habilitado online.  
3. **B8 (Dinheiro)** â€” habilitado **somente** se `GET /cash` retornou `open != null`; senÃ£o botÃ£o visÃ­vel **desabilitado** com tooltip/subtÃ­tulo **S5**.

**Offline:** **B6 e B7 desabilitados**; **B8** permitido **somente** se jÃ¡ havia sessÃ£o de caixa aberta antes de perder rede (estado local `cash_open_cached===true` vindo da Ãºltima `GET /cash` bem-sucedida?) â€” **fechar regra:** se offline, **todos** mÃ©todos desabilitados e alert **S6** (dinheiro tambÃ©m exige API no spec backend para POST cash â€” logo **offline: nenhum pagamento**). Texto **S6** obrigatÃ³rio.

Tap **B6** â†’ `op_pay_pix` com `paymentId`.  
Tap **B7** â†’ `op_pay_card` com `paymentId`.  
Tap **B8** â†’ confirmar diÃ¡logo **D1**; se OK â†’ `POST /payments/cash` body `{ payment_id }`; sucesso **T4** â†’ `op_home`.

---

### 5.7 `op_pay_pix`

**Props:** `paymentId`.

**API:** `POST /payments/pix` body `{ payment_id }`.

**UI:** exibir QR: gerar imagem a partir da string **`qr_code`** (EMV/payload como texto â€” usar lib QR no Web/Android). BotÃ£o secundÃ¡rio **B9** copiar `qr_code` para clipboard; toast **T5** ao copiar.

**Contador:** `expires_at` âˆ’ `Date.now()` a cada 1s UI; ao â‰¤ 0: texto **S7** e botÃ£o **B10 â€œGerar novo QRâ€** que **rechama** `POST /payments/pix` (mesmo `payment_id`).

**Polling:** a cada **2000 ms** chamar `GET /payments/{paymentId}` atÃ©:

- `status === 'PAID'` â†’ toast **T4** â†’ `op_home`, **ou**
- `status === 'EXPIRED'` â†’ parar polling, mostrar **S7** e **B10**, **ou**
- `status === 'FAILED'` â†’ alert **E7** â†’ `op_pay_method`.

**Parada de seguranÃ§a:** apÃ³s **900000 ms** (15 min) desde `onMount` da tela, parar polling e mostrar **S8** + botÃ£o **B10**.

**Android/Web:** ao sair da tela (`onDispose`/`useEffect cleanup`), **cancelar** timer de polling.

---

### 5.8 `op_pay_card`

**Props:** `paymentId`.

**Mostrar** valor: obter via Ãºltimo `GET /payments/{id}` ou estado passado (obrigatÃ³rio ter amount antes de confirmar).

**API:** `POST /payments/card` `{ payment_id, amount }` com `amount` **igual** ao string do DTO (normalizar para nÃºmero decimal com 2 casas no JSON).

**Sucesso:** **T4** â†’ `op_home`.  
**409 `AMOUNT_MISMATCH`:** **E8**.

---

### 5.9 `forbidden`

**Roles:** qualquer autenticado que caiu em rota nÃ£o permitida.  
**ConteÃºdo:** Ã­cone bloqueio, tÃ­tulo **S9**, subtÃ­tulo **S10**, botÃ£o **B11** â†’ shell inicial do role.

---

### 5.10 `mgr_dashboard`

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /dashboard`.

**Cards (ordem fixa):**

1. Faturamento (hoje): valor `faturamento` formatado **R$ #,##0.00** `pt-BR`.  
2. OcupaÃ§Ã£o: `ocupacao` como **percentual** `0.0%`â€“`100.0%` com uma casa decimal.  
3. Check-outs hoje: `tickets_dia` inteiro.  
4. Uso convÃªnio: se `uso_convenio === null` exibir **â€œâ€”â€**; senÃ£o percentual **`(uso_convenio*100).toFixed(1)%`**.

**NavegaÃ§Ã£o:** botÃµes **B22** â†’ `mgr_movements`, **B23** â†’ `mgr_analytics`, **B12** â†’ `mgr_cash`, **B26** â†’ `mgr_lojista_invites` (**somente** ADMIN e SUPER_ADMIN\*; gestor MANAGER nÃ£o vÃª **B26**), **B13** â†’ `mgr_settings`, **B21** â†’ `op_home` (OperaÃ§Ã£o).

**Paridade Web/Android:** no painel (`mgr_dashboard`), **B22** e **B23** existem **nos dois** clientes, na mesma ordem relativa; **B26** tambÃ©m nos dois, condicionado ao role como acima.

---

### 5.10.0 `mgr_lojista_invites` â€” Convites para cadastro de lojista

**Roles:** ADMIN, SUPER_ADMIN\* (alinhado Ã  API `GET/POST .../admin/lojista-invites`). **MANAGER** nÃ£o acessa esta rota (matriz Â§6).

**ConteÃºdo:** mesma UX de geraÃ§Ã£o/listagem de convites descrita em `mgr_settings` para administradores; rota dedicada para acesso direto a partir do painel (**B26**).

**Lista â€œLojistas do estacionamentoâ€:** apÃ³s `GET /admin/lojista-invites`, exibir **todos** os lojistas do tenant. Por linha: **nome da loja** (`shopName`), **cÃ³digo pÃºblico** (ou â€œâ€”â€ se `merchantCode` for `null`), estado **Pendente/Ativado**. Se **Ativado:** **e-mail** da conta, **horas compradas** (`totalPurchasedHours`) e **saldo disponÃ­vel** (`balanceHours`). Se **Pendente:** nÃ£o mostrar e-mail nem campos de horas (valores vÃªm `null` da API).

---

### 5.10.1 `mgr_movements` â€” Extrato com filtros

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /manager/movements`.

**Filtros obrigatÃ³rios na UI:**

- janela rÃ¡pida (**24h**, **7d**, **30d**),
- intervalo manual (`from`/`to`, UTC),
- tipo (`TICKET_PAYMENT`, `PACKAGE_PAYMENT`, `LOJISTA_USAGE`, `CLIENT_USAGE`).

**Exibir:**

- resumo (`total_ticket`, `total_package`, `usages_lojista`, `usages_client`, `count`);
- lista paginada/simples com `at`, `kind`, `amount`, `method`.

BotÃ£o **B23** deve abrir `mgr_analytics`.

---

### 5.10.2 `mgr_analytics` â€” TendÃªncias e horÃ¡rios de pico

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /manager/analytics?days=N` (1..90).

**Exibir:**

- totais do perÃ­odo (`revenue`, `payments`, `checkouts`),
- `trend_by_day`,
- `gains_by_hour`,
- `peak_hours` (top horÃ¡rios).

---

### 5.11 `mgr_cash`

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**Ao abrir:** `GET /cash`.

- Se `open === null`: botÃ£o **B14** `POST /cash/open` â†’ sucesso refresh; **B15** fechar **desabilitado**.  
- Se `open !== null`: exibir `expected_amount` formatado BRL; **B15** habilitado â†’ formulÃ¡rio `actual_amount` (decimal) â†’ `POST /cash/close` `{ session_id: open.session_id, actual_amount }`.  
 Se resposta `alert === true`: apÃ³s fechar modal sucesso, mostrar alert nÃ£o bloqueante **T6** (divergÃªncia).

**Textos:** tÃ­tulo **S11**.

---

### 5.12 `mgr_settings`

**Roles:** MANAGER, ADMIN, SUPER_ADMIN\*.

**API:** `GET /settings` ao abrir; `POST /settings` ao salvar.

**Campos:** `price_per_hour`, `capacity` (inteiro > 0). ValidaÃ§Ã£o cliente: capacity â‰¥ 1; price â‰¥ 0.01.

**Salvar:** toast **T7** ou erro campo.

**Lista de pacotes (somente leitura nesta versÃ£o):** `GET /recharge-packages?scope=CLIENT` e outra chamada `scope=LOJISTA`; exibir duas listas com **horas** e **preÃ§o** â€” texto **S12** se vazio. *(Cadastro/ediÃ§Ã£o de pacotes: FORA DE ESCOPO frontend; dados vÃªm do seed/DB.)*

**Convites lojista (somente ADMIN e SUPER_ADMIN):** secÃ§Ã£o adicional â€” `POST /admin/lojista-invites` (opcional `displayName`) e `GET /admin/lojista-invites` para listar **todos** os lojistas do tenant com `shopName`, `merchantCode` (ou ausente), **Pendente/Ativado**, e quando ativado: `email`, `totalPurchasedHours`, `balanceHours` (ver Â§5.10.0). Na criaÃ§Ã£o, exibir **uma vez** o `activationCode` devolvido (cÃ³pia manual). **SUPER_ADMIN** exige **Â§4.3** (`X-Parking-Id`). **MANAGER** nÃ£o vÃª esta secÃ§Ã£o.

---

### 5.13 `cli_wallet`

**Roles:** CLIENT.

**API:** `GET /client/wallet`.

**Exibir:** saldo horas inteiro, expiraÃ§Ã£o se nÃ£o null (data **dd/MM/yyyy**).

**B16** â†’ `cli_buy`.  
**B17** â†’ `cli_history`.

---

### 5.14 `cli_history`

**Roles:** CLIENT.

**API:** `GET /client/history?limit=50` + `cursor` se `next_cursor` anterior.

**Lista:** cada item: `kind` **â€œCompraâ€** se PURCHASE / **â€œUsoâ€** se USAGE; `delta_hours` com sinal **+** para compra; data formatada.

**Infinite scroll:** ao fim da lista, se `next_cursor` nÃ£o null, prÃ³xima pÃ¡gina.

---

### 5.15 `cli_buy`

**Roles:** CLIENT.

**API:** `GET /recharge-packages?scope=CLIENT`.

**Lista** pacotes: botÃ£o por item **B18** abre escolha: **CrÃ©dito** / **PIX** (modal dois botÃµes).

- **CrÃ©dito:** confirma **D2** â†’ `POST /client/buy` `{ package_id, settlement: "CREDIT" }` + Idempotency â†’ sucesso **T8** â†’ refresh wallet em background, voltar `cli_wallet`.  
- **PIX:** `POST /client/buy` `{ package_id, settlement: "PIX" }` + Idempotency â†’ recebe `payment_id` â†’ navegar `cli_pay_pix` com esse id.

---

### 5.16 `cli_pay_pix`

**Roles:** CLIENT.

Igual `op_pay_pix` (Â§5.7) com mesmas regras de polling e QR; sucesso **T8** â†’ `cli_wallet`.

---

### 5.17 `loj_wallet`, `loj_history`, `loj_buy`, `loj_pay_pix`

**Roles:** LOJISTA.  
**Comportamento:** idÃªntico a cliente substituindo:

- endpoints `/lojista/*`,  
- `GET /recharge-packages?scope=LOJISTA`,  
- settlement e labels **S13** onde falar â€œclienteâ€.

**Carteira (`loj_wallet`):** alÃ©m de **B16** (comprar) e **B17** (histÃ³rico de movimentos da carteira), botÃµes **B27** â†’ `loj_grant` e **B29** â†’ `loj_grant_history`.  
**PreferÃªncia de bonificaÃ§Ã£o:** `GET /lojista/grant-settings` ao abrir; interruptor com **aria-label** **B30**. **Desmarcado** â†’ `allow_grant_before_entry = true` (crÃ©dito antecipado por placa). **Marcado** â†’ `allow_grant_before_entry = false` (sÃ³ com ticket **OPEN** ou **AWAITING_PAYMENT** no estacionamento, ou `ticketId` vÃ¡lido). Ao alterar: `PUT /lojista/grant-settings`. Textos de apoio **S17** / **S18**.

### 5.17.1 `loj_grant` â€” Bonificar cliente

**Roles:** LOJISTA.

**Web:** placa + horas (default **1** editÃ¡vel) + confirmar. **Android:** igual + **B28** abre leitor de QR do cupom; o primeiro UUID vÃ¡lido no texto Ã© enviado como `ticketId` (`POST /lojista/grant-client` com header `Idempotency-Key`). Na app, **placa** e **cupom** sÃ£o mutuamente exclusivos: ao escanear limpa-se a placa; ao editar a placa limpa-se o cupom.

Se `allow_grant_before_entry` for **false**, exibir aviso **S19** na tela (resumo da regra). Erros: **409** `LOJISTA_CREDIT_INSUFFICIENT` â€” mensagem de crÃ©ditos insuficientes; **409** `CLIENT_FOR_OTHER_LOJISTA`; **409** `GRANT_REQUIRES_ACTIVE_TICKET`; demais mapa Â§8. Sucesso: toast **T10** (Android) ou mensagem equivalente no Web.

O saldo bonificado do convÃªnio deve ser apresentado como saldo **separado** da carteira comprada do cliente. No checkout, o consumo segue a ordem: **convÃªnio (hours_lojista)** antes de **carteira do cliente (hours_cliente)**.

ApÃ³s `POST /lojista/grant-client`, a mensagem de sucesso deve refletir `client_balance_hours` retornado pela API (saldo bonificado da **placa**, nÃ£o a carteira comprada), **tambÃ©m** quando a placa jÃ¡ tinha cadastro de cliente no tenant.

### 5.17.2 `loj_grant_history` â€” Extrato de bonificaÃ§Ãµes

**Roles:** LOJISTA.

Lista `GET /lojista/grant-client/history`: data/hora (`created_at`, exibir em UTC), placa, horas concedidas e **modo da bonificacao** (`grant_mode`: `ON_SITE` ou `ADVANCE`). Filtros opcionais: intervalo de datas, placa (query `from`, `to`, `plate`).

---

### 5.18 `adm_tenant` â€” SUPER_ADMIN

**Proibido para ADMIN / MANAGER / OPERATOR:** esta rota existe **apenas** para **SUPER_ADMIN** (matriz Â§6). O **administrador do tenant** (**ADMIN**) inicia em **gestÃ£o** (`mgr_dashboard`) com o `parking_id` do login â€” nÃ£o escolhe estacionamento global nem cria tenant.

**ConteÃºdo (Web e Android):**

1. **Criar estacionamento:** formulÃ¡rio com e-mail e senha do **administrador do tenant** (ADMIN) e e-mail e senha do **primeiro operador** â€” contas **distintas**. Chamada `POST /admin/tenants` (sem `X-Parking-Id` necessÃ¡rio para este POST). Sucesso: mensagem clara; atualizar lista.
2. **Lista:** `GET /admin/tenants`; permitir escolher um item para definir `active_parking_id` / `X-Parking-Id`.
3. **UUID manual (avanÃ§ado):** campo UUID + â€œContinuarâ€ (Â§4.3); apÃ³s vÃ¡lido: **B20** â†’ `mgr_dashboard`, **B21** â†’ `op_home`. Sem tenant ativo: **S15** ao tocar em GestÃ£o/OperaÃ§Ã£o.

---

## 6. Matriz rota de UI Ã— role

| ID rota | OP | MG | AD | CL | LJ | SP |
|---------|:--:|:--:|:--:|:--:|:--:|:--:|
| login | âœ“ | âœ“ | âœ“ | âœ“ | âœ“ | âœ“ |
| loj_register | âœ“Â¹ | âœ“Â¹ | âœ“Â¹ | âœ“Â¹ | âœ“Â¹ | âœ“Â¹ |
| op_home | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| op_entry_plate | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| op_ticket_detail | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| op_checkout | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| op_pay_method | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| op_pay_pix | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| op_pay_card | âœ“ | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| mgr_dashboard | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| mgr_movements | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| mgr_analytics | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| mgr_cash | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| mgr_lojista_invites | âœ— | âœ— | âœ“ | âœ— | âœ— | âœ“* |
| mgr_settings | âœ— | âœ“ | âœ“ | âœ— | âœ— | âœ“* |
| cli_* | âœ— | âœ— | âœ— | âœ“ | âœ— | âœ— |
| loj_* | âœ— | âœ— | âœ— | âœ— | âœ“ | âœ— |
| adm_tenant | âœ— | âœ— | âœ— | âœ— | âœ— | âœ“ |
| forbidden | âœ“ | âœ“ | âœ“ | âœ“ | âœ“ | âœ“ |

\*Requer `active_parking_id` para chamadas API; ver Â§4.3.  
**Â¹** Rota pÃºblica antes da autenticaÃ§Ã£o; se jÃ¡ existir sessÃ£o vÃ¡lida, o guard deve redirecionar ao shell (Â§4.2).

**MANAGER** e **ADMIN** podem usar **tanto** fluxo operador **quanto** gestor: **dois Ã­cones** na navegaÃ§Ã£o principal **TabBar** (Android) ou **sidebar** (Web): â€œOperaÃ§Ã£oâ€ (â†’ `op_home`) e â€œGestÃ£oâ€ (â†’ `mgr_dashboard`). **OPERATOR** sÃ³ â€œOperaÃ§Ã£oâ€. **SUPER_ADMIN**: apÃ³s definir tenant, mesmo padrÃ£o que MANAGER ou sÃ³ gestÃ£o â€” **fixar:** SUPER_ADMIN vÃª **OperaÃ§Ã£o + GestÃ£o** como MANAGER.

---

## 7. Design tokens (fixos)

| Token | Valor | Uso |
|-------|-------|-----|
| `color.primary` | `#1565C0` | BotÃµes primÃ¡rios, links |
| `color.error` | `#C62828` | Erros, alertas crÃ­ticos |
| `color.surface` | `#FFFFFF` | Fundo telas |
| `color.text` | `#212121` | Texto principal |
| `color.text_secondary` | `#757575` | SubtÃ­tulos |
| `space.page` | `16dp` / `16px` | Padding horizontal listas e formulÃ¡rios |
| `touch.min` | `48dp` | Altura mÃ­nima alvo toque |
| `font.title` | 20sp / 1.25rem semibold | TÃ­tulo de tela |
| `font.body` | 16sp / 1rem regular | Corpo |

**Componente primÃ¡rio:** retÃ¢ngulo preenchido `primary`, texto branco `#FFFFFF`.

---

## 8. Mapa de cÃ³digos HTTP â†’ mensagem UX (fallback)

Se `message` vier vazio no JSON:

| code | Texto |
|------|--------|
| VALIDATION_ERROR | Verifique os dados informados. |
| UNAUTHORIZED | SessÃ£o expirada. FaÃ§a login novamente. |
| FORBIDDEN | VocÃª nÃ£o tem permissÃ£o. |
| NOT_FOUND | Registro nÃ£o encontrado. |
| CONFLICT | OperaÃ§Ã£o nÃ£o permitida no estado atual. |
| PLATE_INVALID | Placa invÃ¡lida. |
| PLATE_HAS_ACTIVE_TICKET | JÃ¡ existe ticket aberto para esta placa. |
| INVALID_TICKET_STATE | Ticket nÃ£o estÃ¡ nesta etapa. |
| LOJISTA_WALLET_MISSING | ConvÃªnio indisponÃ­vel: carteira do lojista nÃ£o configurada. |
| PAYMENT_ALREADY_PAID | Pagamento jÃ¡ confirmado. |
| AMOUNT_MISMATCH | Valor nÃ£o confere. |
| CASH_SESSION_REQUIRED | Abra o caixa antes de receber em dinheiro. |
| OPERATOR_BLOCKED | *(ver E1)* |
| TENANT_UNAVAILABLE | Estacionamento indisponÃ­vel. Tente mais tarde. |
| LOGIN_THROTTED | Muitas tentativas. Aguarde e tente novamente. |
| CLOCK_SKEW | RelÃ³gio do aparelho incorreto. Ajuste a data/hora. |
| INTERNAL | Erro no servidor. Tente novamente. |
| LOJISTA_INVITE_INVALID | CÃ³digo do lojista ou ativaÃ§Ã£o invÃ¡lidos. |
| LOJISTA_INVITE_CONSUMED | Este convite jÃ¡ foi utilizado. |
| LOJISTA_CREDIT_INSUFFICIENT | CrÃ©ditos insuficientes na sua carteira de convÃªnio. |
| CLIENT_FOR_OTHER_LOJISTA | Esta placa estÃ¡ vinculada a outro convÃªnio. |
| GRANT_REQUIRES_ACTIVE_TICKET | Ã‰ necessÃ¡rio ticket em aberto para esta placa, ou permita crÃ©dito antecipado na carteira. |

---

## 9. Tabela de strings (literais)

| ID | Texto |
|----|--------|
| B1 | Entrar |
| B24 | Criar conta |
| B25 | Cadastro de lojista |
| B2 | Nova entrada |
| B3 | Registrar problema |
| B4 | Registrar saÃ­da (checkout) |
| B5 | Pagar |
| B6 | PIX |
| B7 | CartÃ£o |
| B8 | Dinheiro |
| B9 | Copiar cÃ³digo PIX |
| B10 | Gerar novo QR |
| B11 | Voltar ao inÃ­cio |
| B12 | Caixa |
| B13 | ConfiguraÃ§Ãµes |
| B14 | Abrir caixa |
| B15 | Fechar caixa |
| B16 | Comprar horas |
| B17 | HistÃ³rico |
| B18 | Selecionar |
| B20 | GestÃ£o |
| B21 | OperaÃ§Ã£o |
| B22 | Insights |
| B23 | AnÃ¡lises |
| B26 | Cadastro de lojistas |
| B27 | Bonificar cliente |
| B28 | Escanear QR do cupom |
| B29 | Extrato de bonificaÃ§Ãµes |
| B30 | SÃ³ bonificar com veÃ­culo no pÃ¡tio |
| D1 | Confirmar recebimento em dinheiro neste valor? |
| D2 | Confirmar compra a crÃ©dito interno? O valor serÃ¡ registrado. |
| S1 | Nenhum veÃ­culo no pÃ¡tio. |
| S2 | Sem conexÃ£o. Algumas aÃ§Ãµes ficam bloqueadas. |
| S3 | Dados podem estar desatualizados (offline). |
| S4 | Ticket encerrado. |
| S5 | Abra o caixa para habilitar dinheiro. |
| S6 | Pagamento online indisponÃ­vel offline. Reconecte-se. |
| S7 | QR expirado. |
| S8 | Tempo limite de espera do pagamento. Use â€œGerar novo QRâ€. |
| S9 | Acesso negado |
| S10 | VocÃª nÃ£o pode abrir esta Ã¡rea com seu perfil. |
| S11 | SessÃ£o de caixa |
| S12 | Nenhum pacote cadastrado para este tipo. |
| S13 | (Lojista) Mesmas aÃ§Ãµes que cliente, textos com â€œsua carteira de convÃªnioâ€. |
| S15 | Informe o ID do estacionamento (UUID) para continuar. |
| S17 | Desligado: vocÃª pode bonificar sÃ³ com a placa, antes da entrada no estacionamento. |
| S18 | Ligado: bonificaÃ§Ã£o sÃ³ com veÃ­culo no pÃ¡tio (ticket em aberto ou aguardando pagamento), ou pelo QR do cupom. |
| S19 | Modo restrito: bonificaÃ§Ã£o exige veÃ­culo no estacionamento (entrada registrada) ou use o cÃ³digo do cupom. |
| T1 | Problema registrado. |
| T2 | Entrada registrada. |
| T3 | SaÃ­da registrada. Nada a pagar. |
| T4 | Pagamento confirmado. |
| T5 | CÃ³digo copiado. |
| T6 | Alerta: divergÃªncia no caixa acima do limite. |
| T7 | ConfiguraÃ§Ãµes salvas. |
| T8 | Compra concluÃ­da. |
| T10 | BonificaÃ§Ã£o registrada. |
| E1 | Operador bloqueado. Procure o gestor. |
| E2 | Aguarde antes de tentar de novo. |
| E3 | Preencha este campo. |
| E9 | O cÃ³digo do lojista deve ter 10 caracteres. |
| E4 | Formato de placa invÃ¡lido. |
| E5 | JÃ¡ existe ticket em aberto para esta placa. |
| E6 | NÃ£o foi possÃ­vel registrar a saÃ­da neste estado. |
| E7 | Pagamento falhou. Escolha outro mÃ©todo ou tente novamente. |
| E8 | Valor enviado nÃ£o confere com o ticket. |

---

## 10. Offline â€” fila (espelho Â§16 backend)

**OperaÃ§Ãµes enfileirÃ¡veis:**

- `POST /tickets`
- `POST /tickets/{id}/checkout`

**Estrutura item da fila:**  
`{ id_local: uuid, method: "POST", path: string, headers: { Idempotency-Key, Authorization }, body: object|null, created_at_epoch: number }`

**Drain:** ao voltar `online`, enviar **FIFO**; **mÃ¡x. 5** tentativas por item com backoff **1s, 2s, 4s, 8s, 16s** entre tentativas daquele item. ApÃ³s falha final: notificar **T9** â€œFila: operaÃ§Ã£o nÃ£o enviadaâ€ + manter item para revisÃ£o manual **FORA DE ESCOPO** UI detalhada â€” **mÃ­nimo:** toast **T9**.

**T9:** NÃ£o foi possÃ­vel sincronizar uma operaÃ§Ã£o. Verifique na lista de tickets.

**Proibido** enfileirar `POST /payments/*`.

---

## 11. Acessibilidade (mÃ­nimo)

- Contraste texto/fundo â‰¥ **4.5:1** para `text` sobre `surface` (tokens Â§7 atendem material padrÃ£o).  
- **Web (Vue):** cada controle clicÃ¡vel `B*` com atributo **`aria-label`** igual ao texto do botÃ£o (literais Â§9).  
- **Android (Compose):** `Modifier.semantics { contentDescription = "..." }` com o mesmo texto **B\***.  
- Campo placa: **Web:** `aria-label="Placa do veÃ­culo"`; **Android:** `label = { Text("Placa do veÃ­culo") }` no `OutlinedTextField`.

---

## 12. ReferÃªncia cruzada backend

| Necessidade frontend | Onde no `SPEC.md` v8.7 |
|----------------------|-------------------------|
| Placas | Â§6 |
| Stack servidor / repo | Â§1.1 |
| TDD / CI / DoD / zero risco | Â§23â€“Â§26, `AGENTS.md` |
| RBAC API | Â§17 |
| DTO pagamento / polling | Â§18 `GET /payments/{id}` |
| Pacotes lista | Â§18 `GET /recharge-packages` |
| Settings leitura | Â§18 `GET /settings` |
| Docker / `.env` | Â§19 + `README.md` |

---

## 13. Qualidade, TDD e definiÃ§Ã£o de pronto (front)

Normativo em conjunto com **`SPEC.md` Â§23**. O utilizador **nÃ£o** valida manualmente cada entrega; **aceite** = suÃ­te automatizada verde + DoD abaixo.

### 13.1 PrincÃ­pios

- **TDD** onde aplicÃ¡vel: teste de componente/composable **falha** â†’ implementaÃ§Ã£o mÃ­nima â†’ refatora.  
- **Nenhum merge** na branch principal com testes falhando ou ignorados sem justificativa (issue + prazo).

### 13.2 Web (Vue 3)

| Tipo | Ferramenta mÃ­nima fixa | Escopo |
|------|------------------------|--------|
| **Unit** | **Vitest 2.x** (Ãºltima minor estÃ¡vel) + **@vue/test-utils** | Composables (`useApi`, stores Pinia), helpers de validaÃ§Ã£o de placa (regex Â§6), parsers de erro |
| **E2E** | **Playwright** (Ãºltima 1.x estÃ¡vel) | Navegador headless Chromium; baseURL `http://localhost:5173`; API real em `VITE_API_BASE` (subir backend + Postgres antes do job) |

**CenÃ¡rios E2E obrigatÃ³rios (mÃ­nimo):**

1. Login (credenciais de fixture em `.env.test` ou seed) â†’ redireciona para shell do role.  
2. **OPERATOR:** lista tickets abertos ou vazia â†’ **Nova entrada** com placa vÃ¡lida â†’ aparece na lista.  
3. **Fluxo pagamento PIX (Stub):** checkout com valor > 0 â†’ tela PIX â†’ **simular** webhook via **API** (curl script chamado no `beforeAll` ou helper HTTP no teste) ou fixture que chama backend â€” assert ticket encerrado ao consultar API ou UI atualizada apÃ³s polling.

**Cobertura:** **â‰¥ 60%** de linhas em `src/` excluindo `main.ts` e assets; falha de CI se abaixo (Coverlet/V8 coverage no Vitest).

### 13.3 Android (Compose)

| Tipo | Ferramenta | Escopo |
|------|------------|--------|
| **Unit** | JUnit 4 + coroutines test | ViewModels, mapeadores DTO |
| **UI** | **Compose UI Test** (debug) | Pelo menos **um** fluxo: login fake (mock servidor com **MockWebServer** OkHttp OU contra API de teste) + tela inicial |

**E2E instrumentado completo** (Firebase Test Lab / dispositivo) â€” **recomendado** na v1; se nÃ£o implementado na primeira entrega, **obrigatÃ³rio** documentar em `README` com data alvo e manter **Compose UI Test** cobrindo telas crÃ­ticas.

### 13.4 DoD â€” front (incremento)

1. `npm run test` (Vitest) e `npm run test:e2e` (Playwright) **verdes** no CI.  
2. Android: `./gradlew test` e `./gradlew connectedDebugAndroidTest` **verdes** quando aplicÃ¡vel ao ambiente CI.  
3. Nenhum `console.error` nÃ£o tratado em build de produÃ§Ã£o (ESLint `no-console` em `warn` ou equivalente).

### 13.5 CI front (obrigatÃ³rio)

Pipeline separado ou jobs na mesma pipeline do backend:

- `frontend-web`: `npm ci`, `npm run build`, `npm run test`, `npx playwright install --with-deps`, `npm run test:e2e` (com serviÃ§os `docker compose up` + API em background).  
- `android`: `./gradlew assembleDebug test` (e connected se runner disponÃ­vel).

**Merge bloqueado** se falhar.

### 13.6 Controles de repositÃ³rio (anti entrega sem testes)

ObrigatÃ³rio seguir **`SPEC.md` Â§25** e ficheiros **`AGENTS.md`**, **`.github/workflows/ci.yml`**, **`.githooks/pre-commit`**, **`.cursor/rules/tdd-entrega-zero-risco.mdc`**. NÃ£o hÃ¡ â€œentregaâ€ sem CI verde e evidÃªncia de testes conforme essas normas.

---

**Fim SPEC FRONTEND v1.5**

