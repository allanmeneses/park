# Manual do Usuario

## Gestor - Web e Android

Este manual resume o fluxo operacional para `MANAGER`, `ADMIN` e `SUPER_ADMIN` com tenant ativo.

### 1) Acessar o tenant

- `SUPER_ADMIN`: em `/admin/tenant`, crie ou selecione o estacionamento e defina o UUID ativo; depois `Gestao` ou `Operacao`.
- `MANAGER`/`ADMIN`: o tenant vem do proprio login (nao usam a criacao de estacionamento).

### 2) Painel

Na tela de gestor (`Painel`) existem os atalhos:

- `Insights`: extrato com filtros de movimentacoes.
- `Analises`: tendencias, horarios de pico e ganhos por horario.
- `Relatorio de saldos` (Web `/gestor/saldos`, Android botao com o mesmo nome): saldo de convênio por lojista; **placas com horas bonificadas por lojistas ainda disponiveis** (so aparecem se saldo &gt; 0); credito **comprado** por placa (com validade quando existir); filtro opcional por placa; listas de placa ordenadas por maior saldo primeiro.
- `Caixa`: abrir/fechar sessao.
- `Configuracoes`: preco por hora e capacidade.

### 3) Insights (extrato com filtros)

- Filtros rapidos: `Ultimas 24h`, `Ultimos 7 dias`, `Ultimos 30 dias`.
- Filtro manual por intervalo UTC (`De`/`Ate`).
- Filtro por tipo:`r`n- Filtro opcional por `lojista_id` (UUID) para ver apenas movimentos de um lojista especifico.`r`n`r`n- Classificacao de composicao no pagamento de ticket:
  - `TICKET_PAYMENT`
  - `PACKAGE_PAYMENT`
  - `LOJISTA_USAGE`
  - `CLIENT_USAGE`

Resumo exibido:

- Total ticket
- Total pacote
- Usos lojista
- Usos cliente
- Quantidade de registros

### 4) Analises

Permite selecionar janela de dias (1..90) e mostra:

- Receita total do periodo
- Quantidade de pagamentos
- Quantidade de check-outs
- Tendencia por dia
- Ganhos por horario (UTC)
- Horarios de pico (top 3 por check-outs)

### 5) Observacao sobre "hoje"

Relatorios no backend usam UTC para agregacao diaria. Em virada de dia local, o valor de "hoje" pode diferir da expectativa local.
# Manual do usuÃ¡rio â€” Sistema de estacionamento

Documento para **qualquer pessoa** operar o sistema no dia a dia. Leia na ordem na **primeira vez**; depois use o Ã­ndice.

**Idioma da interface:** portuguÃªs (Brasil).  
**Clientes:** **navegador (Web)** e **aplicativo Android** â€” mesma lÃ³gica; caminhos Web aparecem entre parÃªnteses quando forem diferentes.

---

## Ãndice

1. [O que este sistema faz](#1-o-que-este-sistema-faz)
2. [Perfis (quem Ã© quem)](#2-perfis-quem-Ã©-quem)
3. [Antes de ligar o estacionamento â€” ordem obrigatÃ³ria](#3-antes-de-ligar-o-estacionamento--ordem-obrigatÃ³ria)
4. [Acesso: Web e Android](#4-acesso-web-e-android)
5. [Super administrador â€” primeiro uso](#5-super-administrador--primeiro-uso)
6. [Gestor â€” ordem do dia](#6-gestor--ordem-do-dia)
7. [Operador â€” fluxo completo de um veÃ­culo](#7-operador--fluxo-completo-de-um-veÃ­culo)
8. [Motorista (conta prÃ³pria)](#8-motorista-conta-prÃ³pria--horas-em-qualquer-estacionamento)
9. [Lojista (convÃªnio)](#9-lojista-convÃªnio)
10. [Pagamento PIX â€” o que esperar](#10-pagamento-pix--o-que-esperar)
11. [Sem internet (operador)](#11-sem-internet-operador)
12. [Mensagens que vocÃª pode ver (e o que fazer)](#12-mensagens-que-vocÃª-pode-ver-e-o-que-fazer)
13. [O que **nÃ£o** se faz pelo aplicativo ou site](#13-o-que-nÃ£o-se-faz-pelo-aplicativo-ou-site)
14. [ReferÃªncia rÃ¡pida â€” telas Web](#14-referÃªncia-rÃ¡pida--telas-web)
15. [Dicas finais](#15-dicas-finais)

---

## 1. O que este sistema faz

- Registra **entrada** e **saÃ­da** de veÃ­culos no pÃ¡tio (**tickets**).
- Calcula **valor a pagar** na saÃ­da (**checkout**).
- Registra **pagamentos**: PIX, cartÃ£o (ambiente de teste/simulaÃ§Ã£o) e **dinheiro** (com **caixa aberto**).
- **Gestor** vÃª **painel** (nÃºmeros do dia), **abre e fecha caixa** e altera **preÃ§o por hora** e **capacidade**.
- **Motorista** (cadastro prÃ³prio) e **lojista** tÃªm **carteira de horas**, **histÃ³rico** e **compra de pacotes** (quando existirem pacotes no sistema).
- Um **super administrador** pode **criar** estacionamento novo ou **escolher** um jÃ¡ existente **na lista** (pelo **nome** do local â€” **cada nome Ã© Ãºnico** no sistema), **sem precisar de cÃ³digos tÃ©cnicos** no site Web.

---

## 2. Perfis (quem Ã© quem)

| Perfil | Para que serve |
|--------|----------------|
| **Operador** | Entrada de veÃ­culos, lista no pÃ¡tio, checkout e cobranÃ§a na saÃ­da. |
| **Gestor (MANAGER)** | Painel, **Insights** (movimentaÃ§Ãµes), **AnÃ¡lises** (tendÃªncias e picos), caixa, configuraÃ§Ãµes (preÃ§o, capacidade), vÃª pacotes em leitura â€” no **seu** estacionamento. |
| **Administrador do tenant (ADMIN)** | **Mesmas** Ã¡reas de gestÃ£o que o gestor (**Insights**, **AnÃ¡lises**, painel, caixa, configuraÃ§Ãµes, e **OperaÃ§Ã£o** se precisar) no **seu** estacionamento; **nÃ£o** cria estacionamento novo (isso Ã© **super admin**). |
| **Motorista** | Carteira **global** de horas (placa na conta), compra, histÃ³rico â€” vÃ¡lida em qualquer estacionamento do sistema. |
| **Lojista** | Igual ao motorista na app, mas para **carteira de convÃªnio** da loja (rotas `/lojista`). |
| **Super administrador** | Acesso a **todos** os estacionamentos; **cria** tenant (admin + operador iniciais); lista e escolhe onde opera. |

Se vocÃª abrir uma Ã¡rea que seu perfil nÃ£o pode usar, verÃ¡ **â€œAcesso negadoâ€** e um botÃ£o **â€œVoltar ao inÃ­cioâ€**.

---

## 3. Antes de ligar o estacionamento â€” ordem obrigatÃ³ria

Siga **esta ordem** na **implantaÃ§Ã£o** (primeira vez). Pular etapas gera erro ou impossibilita pagamento em dinheiro.

### Passo A â€” Infraestrutura (quem mantÃ©m o servidor)

1. **Banco de dados** (PostgreSQL) no ar.  
2. **API** (servidor backend) no ar, com variÃ¡veis de ambiente corretas (senhas, segredos JWT e webhook PIX â€” quem implantou deve documentar para a equipe).  
3. **Site (Web)** apontando para a URL da API.  
4. **App Android** configurado com o endereÃ§o da API (em produÃ§Ã£o isso Ã© definido na instalaÃ§Ã£o/build â€” peÃ§a ao suporte tÃ©cnico).

### Passo B â€” Criar o estacionamento no sistema

**No site (Web), com conta de super administrador:**

1. ApÃ³s o **login**, abra **`/admin/tenant`** (super administrador).  
2. Em **Novo estacionamento**, preencha o **e-mail e senha do administrador do tenant** (perfil ADMIN, gere sÃ³ esse estacionamento) e o **e-mail e senha do primeiro operador** â€” **dois e-mails diferentes** (nÃ£o reutilize um e-mail jÃ¡ registado noutro utilizador).  
3. O **UUID** manual fica em **Identificador tÃ©cnico** â€” em geral deixe **vazio**: o sistema gera o cÃ³digo. SÃ³ suporte tÃ©cnico deve forÃ§ar um UUID quando for o caso.  
4. Toque em **Criar estacionamento**.  
5. Em sucesso, o local **aparece na lista**; escolha-o ou confirme o UUID ativo antes de **GestÃ£o** ou **OperaÃ§Ã£o**.

**Quem Ã© â€œadministradorâ€?** O **ADMIN** Ã© o dono da conta de gestÃ£o **daquele** estacionamento (painel, caixa, configuraÃ§Ãµes). **NÃ£o** cria estacionamentos novos â€” isso Ã© sÃ³ **super administrador**.

**Alternativa (tÃ©cnica):** **Postman** ou script com **`POST /api/v1/admin/tenants`**.

**No aplicativo Android:** o **super administrador** tambÃ©m pode **criar estacionamento** na prÃ³pria app, com o mesmo conjunto de dados do site: e-mail e senha do administrador do tenant e do primeiro operador.

**Anote com seguranÃ§a:** e-mail e senha do **gestor** do novo local. O **cÃ³digo interno** do estacionamento fica na base de dados â€” a equipa de operaÃ§Ã£o na Web **nÃ£o precisa** de o memorizar.

### Passo C â€” Pacotes de recarga (opcional, mas comum)

- Se o negÃ³cio vende **pacotes de horas** para cliente ou lojista, o banco desse estacionamento pode precisar de **dados iniciais (seed)** â€” procedimento tÃ©cnico em `database/seed/` e README do projeto.  
- Sem pacotes cadastrados, as telas podem mostrar: **â€œNenhum pacote cadastrado para este tipo.â€**

### Passo D â€” Contas de usuÃ¡rio

- Ao **criar o estacionamento**, o sistema jÃ¡ regista o **ADMIN** e o **primeiro OPERATOR**.  
- Outros operadores, gestores (MANAGER), clientes e lojistas seguem a polÃ­tica da empresa (cadastro tÃ©cnico ou processo interno).  
- Cada pessoa recebe **e-mail** e **senha** para **Entrar**.

### Passo E â€” Antes de cobrar **dinheiro** no caixa fÃ­sico

1. AlguÃ©m com perfil **Gestor** ou **Administrador** (ou **Super admin** jÃ¡ com UUID definido) deve **abrir o caixa** na tela **SessÃ£o de caixa**.  
2. **Enquanto o caixa estiver fechado**, o botÃ£o **Dinheiro** na cobranÃ§a pode aparecer **desabilitado**, com a dica **â€œAbra o caixa para habilitar dinheiro.â€**

**Resumo da ordem:** servidor â†’ **criar estacionamento (Web super admin ou API)** â†’ (opcional) pacotes â†’ **criar outros utilizadores** (operadores, clientesâ€¦) conforme polÃ­tica da empresa â†’ **abrir caixa** â†’ operadores podem trabalhar com dinheiro.

---

## 4. Acesso: Web e Android

### 4.1 Entrar (login)

1. Abra o **site** ou o **app**.  
2. Preencha **E-mail** e **Senha**.  
3. Toque/clique em **Entrar**.

**Regras:**

- **E-mail** ou **Senha** vazio: o sistema **nÃ£o** chama o servidor; aparece **â€œPreencha este campo.â€** no primeiro campo invÃ¡lido.  
- **Operador bloqueado:** mensagem **â€œOperador bloqueado. Procure o gestor.â€** â€” nÃ£o apaga o que vocÃª digitou.  
- Muitas tentativas erradas: **â€œAguarde antes de tentar novamente.â€** â€” espere e tente de novo.

### 4.2 Onde cada perfil cai apÃ³s o login

| Perfil | Tela inicial |
|--------|----------------|
| Operador | InÃ­cio do operador (lista de veÃ­culos no pÃ¡tio). |
| Gestor / Admin do tenant | Painel do gestor. |
| Motorista | Carteira global do motorista. |
| Lojista | Carteira do lojista. |
| Super administrador | **`/admin/tenant`**: **criar** estacionamento (admin + operador), **escolher** na lista ou UUID, depois **GestÃ£o** ou **OperaÃ§Ã£o**. |

### 4.3 Sair (encerrar sessÃ£o)

- **No site (Web):** em **qualquer ecrÃ£ depois do login**, aparece **Sair** no **topo Ã  direita**. Ao clicar, o programa tenta **avisar o servidor** para invalidar o token de renovaÃ§Ã£o; **em seguida** apaga a sessÃ£o neste navegador (incluindo o estacionamento ativo do super administrador, se aplicÃ¡vel) e **volta ao ecrÃ£ de Entrar**.
- **No aplicativo Android:** **Sair** fica disponÃ­vel nas telas autenticadas do respetivo perfil. Ao tocar, a app tenta encerrar a sessÃ£o tambÃ©m no servidor e volta ao ecrÃ£ de login.
- Use **Sair** quando terminar, principalmente em **computador ou telefone partilhado**.

---

## 5. Super administrador â€” primeiro uso

**Quando usar:** vocÃª administra **vÃ¡rios** estacionamentos e precisa **criar um novo** ou **operar um que jÃ¡ existe**.

### 5.1 Escolher onde trabalhar (site Web â€” sem cÃ³digos tÃ©cnicos)

1. FaÃ§a **login** como super administrador.  
2. Abra **`/admin/tenant`** (tÃ­tulo **Super â€” Estacionamentos**).  
3. No topo, em **â€œEscolher onde trabalharâ€**, abra a lista **Estacionamento**.  
4. Cada linha mostra o **nome do estacionamento** (Ãºnico no sistema) â€” Ã© sÃ³ escolher o local certo. **NÃ£o precisa saber o que Ã© UUID.**  
5. Depois use **GestÃ£o** ou **OperaÃ§Ã£o**.

**Se nÃ£o escolher nenhum** e carregar em **GestÃ£o** ou **OperaÃ§Ã£o**, aparece: **â€œEscolha um estacionamento na lista.â€**

O sistema **lembra** a escolha no **mesmo separador** (F5 mantÃ©m) e, na **Web**, tambÃ©m no **mesmo computador** apÃ³s fechar o browser (**Ãºltimo estacionamento escolhido**), atÃ© fazer **Sair** (logout), que apaga essa memÃ³ria.  
Se aparecer erro **â€œX-Parking-Idâ€¦â€** ao abrir a lista: **atualize a pÃ¡gina** ou use uma versÃ£o recente do site e da API â€” o carregamento da lista **nÃ£o** deve exigir esse cÃ³digo antes de escolher o local.

Se a **lista de estacionamentos** falhar ao carregar, a tela deve mostrar uma mensagem clara em vez de parecer vazia.

**Lista vazia mas o estacionamento jÃ¡ existia:** use **Recarregar lista** no site; confirme que a API (`dotnet run`) e o Postgres sÃ£o os mesmos onde criou o tenant (`DATABASE_URL_IDENTITY`). Mensagens genÃ©ricas sobre â€œcampo obrigatÃ³rioâ€ para super admin costumam indicar **API antiga** ou **cache** â€” **Ctrl+Shift+R** no site e reiniciar a API com o cÃ³digo atual.

### 5.2 Criar um estacionamento novo (site Web)

1. Na mesma pÃ¡gina, mais abaixo, secÃ§Ã£o **Criar novo estacionamento**.  
2. Preencha o **nome** do estacionamento (Ãºnico; se jÃ¡ existir, o sistema recusa), o **e-mail** e a **senha** do **primeiro gestor** desse local.  
3. SÃ³ abra **â€œAvanÃ§ado (suporte tÃ©cnico)â€** se a **TI** pedir um cÃ³digo especÃ­fico.  
4. **Criar estacionamento** â†’ mensagem de sucesso; o novo local fica selecionado na lista.  
5. Use **GestÃ£o** ou **OperaÃ§Ã£o**.

**Se o nome jÃ¡ estiver em uso** ou **o e-mail do gestor jÃ¡ existir no sistema**, o servidor recusa: use **outro nome** ou **outro e-mail** ou peÃ§a ajuda Ã  TI.

### 5.3 SÃ³ a equipa de TI (identificador tÃ©cnico)

- Dentro de **AvanÃ§ado**, existe **identificador tÃ©cnico** + **Definir** â€” para cenÃ¡rios em que a TI manda colar um cÃ³digo. **Utilizadores de operaÃ§Ã£o nÃ£o precisam disto no dia a dia.**

No **Android**, alÃ©m da criaÃ§Ã£o, existe a Ã¡rea **avançada** de **Identificador tÃ©cnico (UUID)** com botÃ£o **Definir**. O cÃ³digo deve ser um **UUID v4** vÃ¡lido; se nÃ£o for, a app mostra **â€œUUID invÃ¡lido.â€**

---

## 6. Gestor â€” ordem do dia

Aplica-se a **gestor (MANAGER)**, **administrador do tenant (ADMIN)** e **super administrador** jÃ¡ com estacionamento ativo â€” os mesmos atalhos de **GestÃ£o** (painel, insights, anÃ¡lises, caixa, configuraÃ§Ãµes).

### 6.1 Painel (GestÃ£o â€” inÃ­cio)

- Mostra **faturamento do dia**, **ocupaÃ§Ã£o**, **check-outs hoje**, **check-outs com crÃ©dito prÃ©-pago (cliente)** e **uso convÃªnio** (quando existir).  
- Atalhos comuns (Web e Android, na medida em que a versÃ£o expÃµe o botÃ£o):
  - **Insights** â†’ extrato de **movimentaÃ§Ãµes** (totais e lista filtrÃ¡vel); no Android/Web atual tambÃ©m dÃ¡ acesso Ã s **AnÃ¡lises** a partir dessa Ã¡rea.
  - **AnÃ¡lises** â†’ tendÃªncias por dia, ganhos por hora e horÃ¡rios de pico (`GET /manager/analytics`).
  - **RelatÃ³rio de saldos** â†’ saldo convÃªnio por lojista, placas com bonificaÃ§Ã£o de lojista ainda disponÃ­vel (sÃ³ com saldo &gt; 0) e crÃ©dito comprado por placa (`GET /manager/balances-report`), com campo para filtrar placa e ordenaÃ§Ã£o por maior saldo nas listas por placa.
  - **VisÃ£o estratÃ©gica** (quando existir na sua versÃ£o Web) â†’ anÃ¡lise por perÃ­odo (filtros de data): indicadores, grÃ¡ficos por hora e dia da semana (em UTC, como no painel), perfil de pagamento, insights automÃ¡ticos em texto, top placas e um extrato resumido no mesmo intervalo (com **Carregar mais** quando houver pÃ¡ginas).
  - **Extrato** / movimentaÃ§Ãµes â†’ lista de **movimentaÃ§Ãµes financeiras** do estacionamento (pagamentos quitados e usos de carteira), com filtros.
  - **Caixa** â†’ sessÃ£o de caixa.  
- **ConfiguraÃ§Ãµes** â†’ no **topo**, link pronto para **cadastro de clientes** (copiar e partilhar); depois preÃ§o por hora, capacidade, regra de validade da bonificaÃ§Ã£o do lojista, histÃ³rico de alteraÃ§Ãµes e listas de pacotes; **ADMIN** e **SUPER_ADMIN** tambÃ©m podem criar, editar, desativar, reativar e excluir pacotes.

### 6.2 Caixa â€” ordem obrigatÃ³ria

1. **Abrir o caixa** no inÃ­cio do turno (**Abrir caixa** / `POST` de abertura no sistema).  
2. Durante o dia, **operadores** podem registrar **Dinheiro** nas cobranÃ§as **somente** com caixa aberto.  
3. No fechamento, use **Fechar caixa**, informe o valor contado (**valor real**) e confirme.  
4. Se o sistema **alertar divergÃªncia** entre o esperado e o informado, siga o **procedimento interno** da empresa (conferÃªncia, segunda contagem, registro).

### 6.3 ConfiguraÃ§Ãµes

- **Cadastro de clientes (motoristas):** no topo da tela aparece o **link completo** para o cliente criar conta (sem pedir UUID). Use **Copiar link** e envie por WhatsApp, QR ou e-mail. No **Android**, se o site público estiver noutro domínio que a API, confira o início do URL no computador (a app monta o link a partir do servidor configurado).
- Ajuste **preÃ§o por hora** e **capacidade** (nÃºmero inteiro **â‰¥ 1**).  
- Salve. Mensagem de sucesso: **â€œConfiguraÃ§Ãµes salvas.â€**  
- **Validade da bonificaÃ§Ã£o do lojista:**  
  - **Desligado**: as horas bonificadas pelo lojista ficam acumuladas por prazo indeterminado.  
  - **Ligado**: a bonificaÃ§Ã£o vale apenas no dia da concessÃ£o; na virada do dia, esse saldo deixa de aparecer como disponÃ­vel no checkout e no relatÃ³rio de saldos.  
  - **PermissÃ£o:** somente **ADMIN** e **SUPER_ADMIN** podem alterar esta regra; **MANAGER** sÃ³ consulta o estado atual.
- **HistÃ³rico de alteraÃ§Ãµes:** a tela mostra quem alterou a configuraÃ§Ã£o, o perfil, a data/hora e o que mudou (de X para Y).
- **Pacotes:** **MANAGER** vÃª a lista em leitura. **ADMIN** e **SUPER_ADMIN** podem manter pacotes na prÃ³pria tela: criar, editar, desativar, reativar e excluir. Se um pacote jÃ¡ tiver sido usado, o sistema manda **desativar** em vez de excluir.
- **Mercado Pago (PSP do estacionamento):** na Web use o link a partir de **Configurações** (`/gestor/psp-mercadopago`); no Android, o botão **Mercado Pago (PSP)** na mesma área. **MANAGER** pode ver o estado; **ADMIN** e **SUPER_ADMIN** gravam. Com **credenciais do estacionamento** desligadas, continuam a valer as variáveis globais do servidor (`MERCADOPAGO_*`). Com **ligadas**, este local usa só a conta Mercado Pago indicada (teste em **SANDBOX** antes de produção). É obrigatório aceitar a responsabilidade ao gravar; **SUPER_ADMIN** deve indicar o **motivo** da alteração. No painel do Mercado Pago, configure o **webhook** para o URL mostrado na tela (inclui o id do estacionamento). O servidor precisa da variável `TENANT_SECRET_ENCRYPTION_KEY` para guardar segredos do tenant.

---

## 7. Operador â€” fluxo completo de um veÃ­culo

### 7.1 Tela inicial (lista)

- A lista atualiza ao **entrar na tela**, ao **voltar** de outras telas e ao **puxar para atualizar** (quando existir).  
- **Nenhum veÃ­culo:** texto **â€œNenhum veÃ­culo no pÃ¡tio.â€**  
- Cada linha mostra a **data e hora de entrada** no **horÃ¡rio de BrasÃ­lia** (formato dia/mÃªs/ano e hora:minuto), **igual** ao detalhe do ticket, e **â€œdecorridoâ€** â€” quanto tempo o veÃ­culo estÃ¡ no pÃ¡tio em **horas, minutos e segundos** (atualiza enquanto estÃ¡ na lista).  
- **Com internet:** o sistema compara a **data** e a **hora** do seu telemÃ³vel ou computador com o servidor. A **data** (em BrasÃ­lia) tem de ser a **mesma** e a hora nÃ£o pode diferir mais de **cinco minutos**. Se estiver errado, aparece uma **mensagem grande a vermelho** a pedir que ajuste data e hora nas **definiÃ§Ãµes do dispositivo**; atÃ© corrigir ou ficar **sem internet**, a aplicaÃ§Ã£o fica **bloqueada**. **Sem internet**, esse controlo nÃ£o Ã© aplicado e usa-se o relÃ³gio local para o tempo decorrido.  
- Se a lista ficar em **â€œCarregandoâ€¦â€** ou mostrar erro: confirme que a **API** estÃ¡ no ar e que o endereÃ§o configurado no front (por exemplo `VITE_API_BASE`) corresponde Ã  porta em que o servidor estÃ¡ a escutar.  
- Toque em uma **linha** para ver o **detalhe do ticket**.

**BotÃµes importantes**

- **Nova entrada** â€” cadastra veÃ­culo que acabou de entrar.  
- **â‹®** / **Registrar problema** â€” envia registro de problema; em sucesso: **â€œProblema registrado.â€**

### 7.2 Nova entrada â€” ordem

1. Toque **Nova entrada**.  
2. Digite a **placa** no campo com formato visual **AAA-XXXX** (o sistema aceita **Mercosul** ou **antigo**; espaÃ§os e hÃ­fens extra sÃ£o ignorados na validaÃ§Ã£o).  
3. Confirme a entrada conforme o botÃ£o da tela.

**Resultados**

- Sucesso: **â€œEntrada registrada.â€** e a lista atualiza.  
- Placa invÃ¡lida: **â€œFormato de placa invÃ¡lido.â€**  
- JÃ¡ existe ticket aberto para essa placa: **â€œJÃ¡ existe ticket em aberto para esta placa.â€**

### 7.3 Detalhe do ticket â€” o que aparece

- Placa; **entrada** e **saÃ­da** (quando existir) no mesmo formato curto atÃ© segundos; **Estadia** â€” tempo entre entrada e agora (ticket ainda aberto) ou entre entrada e saÃ­da (ticket encerrado ou apÃ³s checkout); **status**.  
- **ConvÃªnios (lojistas):** sÃ³ aparecem quando existe **saldo bonificado disponÃ­vel** (horas &gt; 0) para a placa; caso contrÃ¡rio **nÃ£o** hÃ¡ bloco de convÃªnio na tela. Quando houver informaÃ§Ã£o, mostra-se uma **lista** (cada item: nome do lojista, horas disponÃ­veis na saÃ­da; e, se aplicÃ¡vel, total jÃ¡ concedido quando for diferente do disponÃ­vel). A **ordem da lista nÃ£o indica** ordem de consumo entre lojistas no checkout. **Na saÃ­da (checkout),** o sistema aplica nesta ordem: **primeiro** as horas bonificadas pelo convÃªnio (saldo da placa); **depois**, se ainda faltar tempo a cobrir, as horas da **carteira comprada** pelo cliente; **por Ãºltimo**, o que restar vira **valor a pagar** (PIX, cartÃ£o ou dinheiro).

**AÃ§Ãµes conforme o status**

| Status | O que fazer |
|--------|-------------|
| **Aberto** | **Registrar saÃ­da (checkout)** â€” inicia a saÃ­da. |
| **Aguardando pagamento** | **Pagar** â€” o sistema **atualiza a saÃ­da e o valor** para o instante atual e depois abre PIX, cartÃ£o ou dinheiro (Ãºtil se o carro continuou no pÃ¡tio apÃ³s o primeiro checkout ou o pagamento foi adiado). |
| **Encerrado** | SÃ³ leitura: **â€œTicket encerrado.â€** |

### 7.4 Checkout (registrar saÃ­da)

1. No ticket **aberto**, use **Registrar saÃ­da (checkout)**.  
2. O sistema calcula valores no servidor, consumindo **primeiro** o saldo bonificado do convÃªnio da placa, **depois** a carteira comprada do cliente (se existir), e sÃ³ entÃ£o definindo o **valor em dinheiro** a pagar.

**Resultados**

- **Nada a pagar:** mensagem do tipo **â€œSaÃ­da registrada. Nada a pagar.â€** e volta Ã  lista.  
- **Valor a pagar:** abre a tela de **escolha de pagamento** (PIX / CartÃ£o / Dinheiro).

Se o estado do ticket nÃ£o permitir: **â€œNÃ£o foi possÃ­vel registrar a saÃ­da neste estado.â€** â€” volte ao detalhe e verifique o status.

### 7.5 Escolha do pagamento

Ao **abrir** esta tela (por exemplo a partir de um atalho ou voltando ao fluxo), o sistema pode **recalcular** de novo o tempo e o valor do ticket em **aguardando pagamento**, para coincidir com o momento em que o cliente vai pagar de facto. Se, apÃ³s esse recÃ¡lculo, **nÃ£o houver nada a pagar** (por exemplo horas cobertas pelo **convÃªnio do lojista** ou pela carteira), a aplicaÃ§Ã£o **volta Ã  lista** com mensagem de saÃ­da registada â€” **nÃ£o** Ã© necessÃ¡rio concluir PIX, cartÃ£o ou dinheiro.

**Antes de tudo:** para **Dinheiro**, o **caixa precisa estar aberto** (gestor). Caso contrÃ¡rio, **Dinheiro** fica desabilitado e aparece **â€œAbra o caixa para habilitar dinheiro.â€**

| MÃ©todo | O que acontece |
|--------|----------------|
| **PIX** | Abre tela com **QR** e opÃ§Ãµes de copiar cÃ³digo / gerar novo QR se expirar. |
| **CartÃ£o** | Com **PSP em modo teste/simulaÃ§Ã£o**, confirmaÃ§Ã£o imediata no app. Com **Mercado Pago** (produÃ§Ã£o), o servidor devolve um **link de checkout** com valor jÃ¡ fixo: o operador abre esse link no dispositivo; o pagamento sÃ³ fica **confirmado** quando o PSP notifica o servidor (pode demorar alguns segundos â€” a app deve atualizar o estado do pagamento). |
| **Dinheiro** | Pede confirmaÃ§Ã£o **â€œConfirmar recebimento em dinheiro neste valor?â€** â€” ao confirmar, registra no sistema. |

**Sem internet:** em geral **nÃ£o** Ã© possÃ­vel concluir pagamento online; aparece **â€œPagamento online indisponÃ­vel offline. Reconecte-se.â€** (regra da versÃ£o atual).

**Sucesso comum:** **â€œPagamento confirmado.â€** e retorno Ã  lista.

---

## 8. Motorista (conta prÃ³pria â€” horas em qualquer estacionamento)

Este perfil Ã© **sÃ³ para quem estaciona o carro**: cria a **prÃ³pria conta** com **e-mail**, **senha** e **placa**. As **horas compradas** ficam numa **carteira global**; ao sair de um estacionamento, se o **ticket** for da **mesma placa**, o sistema pode **usar** essas horas automaticamente (alÃ©m das regras do local). O motorista **nÃ£o** usa as Ã¡reas de **operador**, **gestor** nem **super admin**.

### Ordem sugerida

1. **Criar conta** (primeira vez): use o **link de cadastro** do estacionamento (a partir do **login** â†’ **Cadastro de cliente** na Web ou na app) e informe **placa do veÃ­culo** (o campo mostra o formato **AAA-XXXX**; espaÃ§os e hÃ­fens extra sÃ£o ignorados na validaÃ§Ã£o), **e-mail** e **senha**. Depois **entre** com o mesmo e-mail e senha.  
2. **Carteira:** vÃª **saldo de horas**, **placa** associada e **validade**, se houver.  
3. **Comprar horas:** lista de pacotes â†’ **Selecionar** â†’ escolher a forma de pagamento.  
   - **PIX** fica activo e leva para a tela do QR.  
   - **CartÃ£o** aparece como **em breve** e fica desabilitado.  
4. **HistÃ³rico:** movimentos da carteira global (compras, usos no estacionamento, etc.), com â€œcarregar maisâ€ se existir.

**PIX na compra:** igual Ã  ideia da tela de PIX do operador (QR, espera do pagamento, **Gerar novo QR** se expirar).

### Caminhos Web (referÃªncia)

- Cadastro de cliente: o motorista deve abrir o **link com o identificador do estacionamento** que a gestão partilhar (ex.: **`/cadastro/cliente/{UUID}`**). Não há campo para o cliente introduzir “ID do estacionamento”; sem esse link, a página explica que é preciso pedi-lo ao estacionamento.  
- Carteira: `/motorista` (o endereÃ§o antigo `/cliente` redireciona para aqui)  
- HistÃ³rico: `/motorista/historico`  
- Comprar: `/motorista/comprar`  
- PIX: `/motorista/pix/:id` (aberto pelo fluxo de compra)

**Android:** no **login**, use **Cadastro de cliente**; apÃ³s entrar, a app abre a carteira do cliente (mesma lÃ³gica da Web).

---

## 9. Lojista (convÃªnio)

### Criar conta (cadastro)

1. Obtenha com a **gestÃ£o do estacionamento** o **cÃ³digo do lojista** (10 caracteres) e o **cÃ³digo de activaÃ§Ã£o** gerados para a sua loja.  
2. Na Web, abra **`/cadastro/lojista`** (ou no app Android, no **login**, **Cadastro de lojista**).  
3. Preencha **cÃ³digo do lojista**, **cÃ³digo de activaÃ§Ã£o**, **nome da loja**, **e-mail** e **senha**.  
4. Envio bem-sucedido: a conta entra directamente na carteira do lojista.

**Mesmo e-mail que motorista global:** se vocÃª jÃ¡ tiver conta de **motorista** (portal do condutor) com esse e-mail, o sistema pode **vincular** a conta do convÃªnio Ã  mesma identidade, mantendo uma senha coerente com o que definiu no cadastro lojista â€” use a **mesma senha** que desejar para os dois fluxos, conforme orientaÃ§Ã£o da sua empresa.

**ApÃ³s entrar:** perfis **CLIENT** com convÃªnio precisam que o aplicativo envie o estacionamento correto nas operaÃ§Ãµes da carteira convÃªnio (a app faz isso automaticamente a partir do seu acesso). Rotas da carteira: **`/lojista`**, **`/lojista/historico`**, **`/lojista/comprar`**, **`/lojista/pix/...`** â€” fluxo semelhante ao **motorista**, com textos de **convÃªnio**.

---

### Uso diÃ¡rio

Fluxo semelhante ao **motorista**: **saldo de horas** da loja, **histÃ³rico**, **compra** de crÃ©ditos para beneficiar veÃ­culos (regras de valores e horas personalizÃ¡veis conforme polÃ­tica do estacionamento â€” ver especificaÃ§Ã£o tÃ©cnica do projeto).

---

## 10. Pagamento PIX â€” o que esperar

1. Abre-se a tela com **QR** (imagem) gerada a partir do cÃ³digo recebido do servidor.  
2. O cliente paga no app do banco **escaneando o QR** ou usando o **copia e cola** (**Copiar cÃ³digo PIX** â†’ **â€œCÃ³digo copiado.â€**).  
3. O sistema **consulta o pagamento automaticamente** em intervalos curtos atÃ© **confirmar**, **expirar** ou **falhar**.  
   - Ao voltar do app do banco para o app/site, a consulta Ã© retomada imediatamente; quando o pagamento estiver **confirmado**, a tela PIX deve fechar sozinha e voltar ao fluxo normal.
4. Se o QR **expirar:** **â€œQR expirado.â€** â€” use **Gerar novo QR**.  
5. ApÃ³s muito tempo na mesma tela, pode aparecer limite de espera: **â€œTempo limite de espera do pagamento. Use â€˜Gerar novo QRâ€™.â€**  
6. **Pagamento falhou:** **â€œPagamento falhou. Escolha outro mÃ©todo ou tente novamente.â€** â€” volte Ã  escolha de mÃ©todo.

**Ambiente de teste (desenvolvimento):** o â€œbancoâ€ pode ser simulado; quem mantÃ©m o servidor confirma o pagamento com **webhook** tÃ©cnico. Em **produÃ§Ã£o com banco real**, o provedor PIX envia a confirmaÃ§Ã£o automaticamente â€” o operador sÃ³ precisa de **internet estÃ¡vel** na hora.

---

## 11. Sem internet (operador)

- Aparece aviso: **â€œSem conexÃ£o. Algumas aÃ§Ãµes ficam bloqueadas.â€**  
- **Nova entrada** pode ficar **desabilitada** ou enfileirada conforme a versÃ£o: se enfileirar, ao voltar a rede o sistema tenta enviar.  
- Se algo **nÃ£o sincronizar** apÃ³s vÃ¡rias tentativas: **â€œNÃ£o foi possÃ­vel sincronizar uma operaÃ§Ã£o. Verifique na lista de tickets.â€**

**RecomendaÃ§Ã£o:** sempre que possÃ­vel, **reconecte** antes de **checkout** e **pagamento**.

---

## 12. Mensagens que vocÃª pode ver (e o que fazer)

| Mensagem | O que fazer |
|----------|-------------|
| Preencha este campo | Complete e-mail ou senha (ou outro campo indicado). |
| Operador bloqueadoâ€¦ | Fale com o **gestor**; desbloqueio Ã© **fora do app** nesta versÃ£o. |
| Aguarde antes de tentarâ€¦ | Espere alguns minutos antes de novo login. |
| Formato de placa invÃ¡lido | Corrija a placa (Mercosul ou antiga). |
| JÃ¡ existe ticket em abertoâ€¦ | Use o ticket existente ou resolva com supervisÃ£o. |
| NÃ£o foi possÃ­vel registrar a saÃ­daâ€¦ | Atualize a tela; se persistir, suporte. |
| Pagamento falhouâ€¦ | Tente outro mÃ©todo ou novo PIX. |
| Valor enviado nÃ£o confereâ€¦ | Na tela de cartÃ£o, confira o valor exibido pelo sistema. |
| Abra o caixa para habilitar dinheiro | PeÃ§a ao **gestor** para **abrir o caixa**. |
| Acesso negado | Use **Voltar ao inÃ­cio**; vocÃª nÃ£o tem perfil para aquela Ã¡rea. |
| Escolha um estacionamento na lista | No site, super admin: abra a lista no topo e selecione um local antes de **GestÃ£o**/**OperaÃ§Ã£o**. |
| Nome do estacionamento: entre 2 e 120 caracteres | Ajuste o nome (nem vazio nem demasiado longo). |
| (mensagem de conflito do servidor sobre nome) | Escolha **outro nome** para o estacionamento â€” cada um deve ser **Ãºnico**. |
| Dados podem estar desatualizados (offline) | Reconecte e atualize a lista. |

---

## 13. O que **nÃ£o** se faz pelo aplicativo ou site

| Necessidade | Onde costuma ser feito |
|-------------|-------------------------|
| **Criar** um estacionamento novo | **Site Web** ou **Android** â€” **Criar estacionamento** em `adm_tenant`; ou API / Postman. |
| **Desbloquear** operador suspenso | API / processo administrativo â€” **nÃ£o** hÃ¡ botÃ£o no app na v1. |
| **Cadastrar ou editar pacotes** pela interface | DisponÃ­vel em **ConfiguraÃ§Ãµes** para **ADMIN** e **SUPER_ADMIN**. |
| **ImpressÃ£o fiscal / maquininha fÃ­sica / cancela automÃ¡tica** | Fora do escopo deste software conforme especificaÃ§Ã£o do projeto. |

---

## 14. ReferÃªncia rÃ¡pida â€” telas Web

| EndereÃ§o (apÃ³s o domÃ­nio do site) | Uso |
|-----------------------------------|-----|
| `/login` | Entrar |
| `/operador` | InÃ­cio operador |
| `/operador/entrada` | Nova entrada |
| `/operador/ticket/:id` | Detalhe do ticket |
| `/operador/checkout/:ticketId` | Checkout |
| `/operador/pagar/:paymentId` | Escolher pagamento |
| `/operador/pix/:paymentId` | PIX |
| `/operador/cartao/:paymentId` | CartÃ£o |
| `/gestor` | Painel |
| `/gestor/visao` | VisÃ£o estratÃ©gica (anÃ¡lise e extrato por perÃ­odo) |
| `/gestor/movimentos` | Extrato de movimentaÃ§Ãµes (gestÃ£o) |
| `/gestor/saldos` | RelatÃ³rio de saldos (lojista + cliente por placa) |
| `/gestor/caixa` | Caixa |
| `/gestor/config` | ConfiguraÃ§Ãµes |
| `/gestor/psp-mercadopago` | Mercado Pago (PSP do estacionamento) |
| `/cadastro/cliente`, `/cliente`, `/cliente/historico`, `/cliente/comprar` | Cliente (cadastro e carteira) |
| `/cadastro/lojista` | Cadastro de conta lojista (convÃªnio) â€” cÃ³digo do lojista + ativaÃ§Ã£o |
| `/lojista`, â€¦ | Carteira e fluxos do lojista |
| `/admin/tenant` | Super admin â€” lista de estacionamentos (por nome Ãºnico) + criar novo |
| `/proibido` | Acesso negado |

---

## 15. Dicas finais

1. **Guarde** o **UUID** de cada estacionamento em local seguro (sÃ³ para quem precisa).  
2. **Abra o caixa** antes de esperar **dinheiro** nas cobranÃ§as.  
3. Em dÃºvida no fluxo de saÃ­da, siga sempre: **lista â†’ ticket â†’ checkout â†’ pagamento â†’ confirmado**.  
4. Problemas que nÃ£o somem com **atualizar** ou **sair e entrar de novo**: abra chamado com **print da mensagem** e **horÃ¡rio** â€” ajuda o suporte tÃ©cnico.

---

*Manual alinhado Ã  interface descrita em `SPEC_FRONTEND.md` v1.13. Ajuste datas, nomes de domÃ­nio e procedimentos internos da sua empresa neste documento se necessÃ¡rio.*

