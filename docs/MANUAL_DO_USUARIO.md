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
- `Caixa`: abrir/fechar sessao.
- `Configuracoes`: preco por hora e capacidade.

### 3) Insights (extrato com filtros)

- Filtros rapidos: `Ultimas 24h`, `Ultimos 7 dias`, `Ultimos 30 dias`.
- Filtro manual por intervalo UTC (`De`/`Ate`).
- Filtro por tipo:
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
# Manual do usuário — Sistema de estacionamento

Documento para **qualquer pessoa** operar o sistema no dia a dia. Leia na ordem na **primeira vez**; depois use o índice.

**Idioma da interface:** português (Brasil).  
**Clientes:** **navegador (Web)** e **aplicativo Android** — mesma lógica; caminhos Web aparecem entre parênteses quando forem diferentes.

---

## Índice

1. [O que este sistema faz](#1-o-que-este-sistema-faz)
2. [Perfis (quem é quem)](#2-perfis-quem-é-quem)
3. [Antes de ligar o estacionamento — ordem obrigatória](#3-antes-de-ligar-o-estacionamento--ordem-obrigatória)
4. [Acesso: Web e Android](#4-acesso-web-e-android)
5. [Super administrador — primeiro uso](#5-super-administrador--primeiro-uso)
6. [Gestor — ordem do dia](#6-gestor--ordem-do-dia)
7. [Operador — fluxo completo de um veículo](#7-operador--fluxo-completo-de-um-veículo)
8. [Motorista (conta própria)](#8-motorista-conta-própria--horas-em-qualquer-estacionamento)
9. [Lojista (convênio)](#9-lojista-convênio)
10. [Pagamento PIX — o que esperar](#10-pagamento-pix--o-que-esperar)
11. [Sem internet (operador)](#11-sem-internet-operador)
12. [Mensagens que você pode ver (e o que fazer)](#12-mensagens-que-você-pode-ver-e-o-que-fazer)
13. [O que **não** se faz pelo aplicativo ou site](#13-o-que-não-se-faz-pelo-aplicativo-ou-site)
14. [Referência rápida — telas Web](#14-referência-rápida--telas-web)
15. [Dicas finais](#15-dicas-finais)

---

## 1. O que este sistema faz

- Registra **entrada** e **saída** de veículos no pátio (**tickets**).
- Calcula **valor a pagar** na saída (**checkout**).
- Registra **pagamentos**: PIX, cartão (ambiente de teste/simulação) e **dinheiro** (com **caixa aberto**).
- **Gestor** vê **painel** (números do dia), **abre e fecha caixa** e altera **preço por hora** e **capacidade**.
- **Motorista** (cadastro próprio) e **lojista** têm **carteira de horas**, **histórico** e **compra de pacotes** (quando existirem pacotes no sistema).
- Um **super administrador** pode **criar** estacionamento novo ou **escolher** um já existente **na lista** (pelo **nome** do local — **cada nome é único** no sistema), **sem precisar de códigos técnicos** no site Web.

---

## 2. Perfis (quem é quem)

| Perfil | Para que serve |
|--------|----------------|
| **Operador** | Entrada de veículos, lista no pátio, checkout e cobrança na saída. |
| **Gestor (MANAGER)** | Painel, **Insights** (movimentações), **Análises** (tendências e picos), caixa, configurações (preço, capacidade), vê pacotes em leitura — no **seu** estacionamento. |
| **Administrador do tenant (ADMIN)** | **Mesmas** áreas de gestão que o gestor (**Insights**, **Análises**, painel, caixa, configurações, e **Operação** se precisar) no **seu** estacionamento; **não** cria estacionamento novo (isso é **super admin**). |
| **Motorista** | Carteira **global** de horas (placa na conta), compra, histórico — válida em qualquer estacionamento do sistema. |
| **Lojista** | Igual ao motorista na app, mas para **carteira de convênio** da loja (rotas `/lojista`). |
| **Super administrador** | Acesso a **todos** os estacionamentos; **cria** tenant (admin + operador iniciais); lista e escolhe onde opera. |

Se você abrir uma área que seu perfil não pode usar, verá **“Acesso negado”** e um botão **“Voltar ao início”**.

---

## 3. Antes de ligar o estacionamento — ordem obrigatória

Siga **esta ordem** na **implantação** (primeira vez). Pular etapas gera erro ou impossibilita pagamento em dinheiro.

### Passo A — Infraestrutura (quem mantém o servidor)

1. **Banco de dados** (PostgreSQL) no ar.  
2. **API** (servidor backend) no ar, com variáveis de ambiente corretas (senhas, segredos JWT e webhook PIX — quem implantou deve documentar para a equipe).  
3. **Site (Web)** apontando para a URL da API.  
4. **App Android** configurado com o endereço da API (em produção isso é definido na instalação/build — peça ao suporte técnico).

### Passo B — Criar o estacionamento no sistema

**No site (Web), com conta de super administrador:**

1. Após o **login**, abra **`/admin/tenant`** (super administrador).  
2. Em **Novo estacionamento**, preencha o **e-mail e senha do administrador do tenant** (perfil ADMIN, gere só esse estacionamento) e o **e-mail e senha do primeiro operador** — **dois e-mails diferentes** (não reutilize um e-mail já registado noutro utilizador).  
3. O **UUID** manual fica em **Identificador técnico** — em geral deixe **vazio**: o sistema gera o código. Só suporte técnico deve forçar um UUID quando for o caso.  
4. Toque em **Criar estacionamento**.  
5. Em sucesso, o local **aparece na lista**; escolha-o ou confirme o UUID ativo antes de **Gestão** ou **Operação**.

**Quem é “administrador”?** O **ADMIN** é o dono da conta de gestão **daquele** estacionamento (painel, caixa, configurações). **Não** cria estacionamentos novos — isso é só **super administrador**.

**Alternativa (técnica):** **Postman** ou script com **`POST /api/v1/admin/tenants`**.

**No aplicativo Android:** **não** há criação na app; use o **site** ou a API. Para operar no Android como super, pode ser necessário o fluxo com **identificador** conforme a versão da app.

**Anote com segurança:** e-mail e senha do **gestor** do novo local. O **código interno** do estacionamento fica na base de dados — a equipa de operação na Web **não precisa** de o memorizar.

### Passo C — Pacotes de recarga (opcional, mas comum)

- Se o negócio vende **pacotes de horas** para cliente ou lojista, o banco desse estacionamento pode precisar de **dados iniciais (seed)** — procedimento técnico em `database/seed/` e README do projeto.  
- Sem pacotes cadastrados, as telas podem mostrar: **“Nenhum pacote cadastrado para este tipo.”**

### Passo D — Contas de usuário

- Ao **criar o estacionamento**, o sistema já regista o **ADMIN** e o **primeiro OPERATOR**.  
- Outros operadores, gestores (MANAGER), clientes e lojistas seguem a política da empresa (cadastro técnico ou processo interno).  
- Cada pessoa recebe **e-mail** e **senha** para **Entrar**.

### Passo E — Antes de cobrar **dinheiro** no caixa físico

1. Alguém com perfil **Gestor** ou **Administrador** (ou **Super admin** já com UUID definido) deve **abrir o caixa** na tela **Sessão de caixa**.  
2. **Enquanto o caixa estiver fechado**, o botão **Dinheiro** na cobrança pode aparecer **desabilitado**, com a dica **“Abra o caixa para habilitar dinheiro.”**

**Resumo da ordem:** servidor → **criar estacionamento (Web super admin ou API)** → (opcional) pacotes → **criar outros utilizadores** (operadores, clientes…) conforme política da empresa → **abrir caixa** → operadores podem trabalhar com dinheiro.

---

## 4. Acesso: Web e Android

### 4.1 Entrar (login)

1. Abra o **site** ou o **app**.  
2. Preencha **E-mail** e **Senha**.  
3. Toque/clique em **Entrar**.

**Regras:**

- **E-mail** ou **Senha** vazio: o sistema **não** chama o servidor; aparece **“Preencha este campo.”** no primeiro campo inválido.  
- **Operador bloqueado:** mensagem **“Operador bloqueado. Procure o gestor.”** — não apaga o que você digitou.  
- Muitas tentativas erradas: **“Aguarde antes de tentar novamente.”** — espere e tente de novo.

### 4.2 Onde cada perfil cai após o login

| Perfil | Tela inicial |
|--------|----------------|
| Operador | Início do operador (lista de veículos no pátio). |
| Gestor / Admin do tenant | Painel do gestor. |
| Motorista | Carteira global do motorista. |
| Lojista | Carteira do lojista. |
| Super administrador | **`/admin/tenant`**: **criar** estacionamento (admin + operador), **escolher** na lista ou UUID, depois **Gestão** ou **Operação**. |

### 4.3 Sair (encerrar sessão)

- **No site (Web):** em **qualquer ecrã depois do login**, aparece **Sair** no **topo à direita**. Ao clicar, o programa tenta **avisar o servidor** para invalidar o token de renovação; **em seguida** apaga a sessão neste navegador (incluindo o estacionamento ativo do super administrador, se aplicável) e **volta ao ecrã de Entrar**.
- **No aplicativo Android:** **Sair** fica na **barra superior** em todas as áreas autenticadas. O efeito é o mesmo: sessão terminada e ecrã de login.
- Use **Sair** quando terminar, principalmente em **computador ou telefone partilhado**.

---

## 5. Super administrador — primeiro uso

**Quando usar:** você administra **vários** estacionamentos e precisa **criar um novo** ou **operar um que já existe**.

### 5.1 Escolher onde trabalhar (site Web — sem códigos técnicos)

1. Faça **login** como super administrador.  
2. Abra **`/admin/tenant`** (título **Super — Estacionamentos**).  
3. No topo, em **“Escolher onde trabalhar”**, abra a lista **Estacionamento**.  
4. Cada linha mostra o **nome do estacionamento** (único no sistema) — é só escolher o local certo. **Não precisa saber o que é UUID.**  
5. Depois use **Gestão** ou **Operação**.

**Se não escolher nenhum** e carregar em **Gestão** ou **Operação**, aparece: **“Escolha um estacionamento na lista.”**

O sistema **lembra** a escolha no **mesmo separador** (F5 mantém) e, na **Web**, também no **mesmo computador** após fechar o browser (**último estacionamento escolhido**), até fazer **Sair** (logout), que apaga essa memória.  
Se aparecer erro **“X-Parking-Id…”** ao abrir a lista: **atualize a página** ou use uma versão recente do site e da API — o carregamento da lista **não** deve exigir esse código antes de escolher o local.

**Lista vazia mas o estacionamento já existia:** use **Recarregar lista** no site; confirme que a API (`dotnet run`) e o Postgres são os mesmos onde criou o tenant (`DATABASE_URL_IDENTITY`). Mensagens genéricas sobre “campo obrigatório” para super admin costumam indicar **API antiga** ou **cache** — **Ctrl+Shift+R** no site e reiniciar a API com o código atual.

### 5.2 Criar um estacionamento novo (site Web)

1. Na mesma página, mais abaixo, secção **Criar novo estacionamento**.  
2. Preencha o **nome** do estacionamento (único; se já existir, o sistema recusa), o **e-mail** e a **senha** do **primeiro gestor** desse local.  
3. Só abra **“Avançado (suporte técnico)”** se a **TI** pedir um código específico.  
4. **Criar estacionamento** → mensagem de sucesso; o novo local fica selecionado na lista.  
5. Use **Gestão** ou **Operação**.

**Se o nome já estiver em uso** ou **o e-mail do gestor já existir no sistema**, o servidor recusa: use **outro nome** ou **outro e-mail** ou peça ajuda à TI.

### 5.3 Só a equipa de TI (identificador técnico)

- Dentro de **Avançado**, existe **identificador técnico** + **Definir** — para cenários em que a TI manda colar um código. **Utilizadores de operação não precisam disto no dia a dia.**

No **Android**, o fluxo continua a pedir o **identificador** do estacionamento como antes (ou evolução futura da app); **criar** estacionamento faz-se pelo **site** ou pela API.

---

## 6. Gestor — ordem do dia

Aplica-se a **gestor (MANAGER)**, **administrador do tenant (ADMIN)** e **super administrador** já com estacionamento ativo — os mesmos atalhos de **Gestão** (painel, insights, análises, caixa, configurações).

### 6.1 Painel (Gestão — início)

- Mostra **faturamento do dia**, **ocupação**, **check-outs hoje**, **check-outs com crédito pré-pago (cliente)** e **uso convênio** (quando existir).  
- Atalhos comuns (Web e Android, na medida em que a versão expõe o botão):
  - **Insights** → extrato de **movimentações** (totais e lista filtrável); no Android/Web atual também dá acesso às **Análises** a partir dessa área.
  - **Análises** → tendências por dia, ganhos por hora e horários de pico (`GET /manager/analytics`).
  - **Visão estratégica** (quando existir na sua versão Web) → análise por período (filtros de data): indicadores, gráficos por hora e dia da semana (em UTC, como no painel), perfil de pagamento, insights automáticos em texto, top placas e um extrato resumido no mesmo intervalo (com **Carregar mais** quando houver páginas).
  - **Extrato** / movimentações → lista de **movimentações financeiras** do estacionamento (pagamentos quitados e usos de carteira), com filtros.
  - **Caixa** → sessão de caixa.  
  - **Configurações** → preço por hora, capacidade e listas de pacotes (somente leitura).

### 6.2 Caixa — ordem obrigatória

1. **Abrir o caixa** no início do turno (**Abrir caixa** / `POST` de abertura no sistema).  
2. Durante o dia, **operadores** podem registrar **Dinheiro** nas cobranças **somente** com caixa aberto.  
3. No fechamento, use **Fechar caixa**, informe o valor contado (**valor real**) e confirme.  
4. Se o sistema **alertar divergência** entre o esperado e o informado, siga o **procedimento interno** da empresa (conferência, segunda contagem, registro).

### 6.3 Configurações

- Ajuste **preço por hora** e **capacidade** (número inteiro **≥ 1**).  
- Salve. Mensagem de sucesso: **“Configurações salvas.”**  
- **Pacotes:** só aparecem listados; **cadastro de pacotes pela tela não existe nesta versão** — alteração é feita por processo técnico/banco.

---

## 7. Operador — fluxo completo de um veículo

### 7.1 Tela inicial (lista)

- A lista atualiza ao **entrar na tela**, ao **voltar** de outras telas e ao **puxar para atualizar** (quando existir).  
- **Nenhum veículo:** texto **“Nenhum veículo no pátio.”**  
- Cada linha mostra a **data e hora de entrada** no relógio do aparelho, até **segundos** (formato tipo dia/mês/ano e hora:minuto:segundo), e **“Estadia”** — quanto tempo o veículo está no pátio em **horas, minutos e segundos** (o valor atualiza enquanto você está na lista).  
- Se a lista ficar em **“Carregando…”** ou mostrar erro: confirme que a **API** está no ar e que o endereço configurado no front (por exemplo `VITE_API_BASE`) corresponde à porta em que o servidor está a escutar.  
- Toque em uma **linha** para ver o **detalhe do ticket**.

**Botões importantes**

- **Nova entrada** — cadastra veículo que acabou de entrar.  
- **⋮** / **Registrar problema** — envia registro de problema; em sucesso: **“Problema registrado.”**

### 7.2 Nova entrada — ordem

1. Toque **Nova entrada**.  
2. Digite a **placa** (o sistema aceita formato **Mercosul** ou **antigo**, letras e números; espaços e hífens são ignorados na validação).  
3. Confirme a entrada conforme o botão da tela.

**Resultados**

- Sucesso: **“Entrada registrada.”** e a lista atualiza.  
- Placa inválida: **“Formato de placa inválido.”**  
- Já existe ticket aberto para essa placa: **“Já existe ticket em aberto para esta placa.”**

### 7.3 Detalhe do ticket — o que aparece

- Placa; **entrada** e **saída** (quando existir) no mesmo formato curto até segundos; **Estadia** — tempo entre entrada e agora (ticket ainda aberto) ou entre entrada e saída (ticket encerrado ou após checkout); **status**.

**Ações conforme o status**

| Status | O que fazer |
|--------|-------------|
| **Aberto** | **Registrar saída (checkout)** — inicia a saída. |
| **Aguardando pagamento** | **Pagar** — escolhe PIX, cartão ou dinheiro. |
| **Encerrado** | Só leitura: **“Ticket encerrado.”** |

### 7.4 Checkout (registrar saída)

1. No ticket **aberto**, use **Registrar saída (checkout)**.  
2. O sistema calcula valores no servidor.

**Resultados**

- **Nada a pagar:** mensagem do tipo **“Saída registrada. Nada a pagar.”** e volta à lista.  
- **Valor a pagar:** abre a tela de **escolha de pagamento** (PIX / Cartão / Dinheiro).

Se o estado do ticket não permitir: **“Não foi possível registrar a saída neste estado.”** — volte ao detalhe e verifique o status.

### 7.5 Escolha do pagamento

**Antes de tudo:** para **Dinheiro**, o **caixa precisa estar aberto** (gestor). Caso contrário, **Dinheiro** fica desabilitado e aparece **“Abra o caixa para habilitar dinheiro.”**

| Método | O que acontece |
|--------|----------------|
| **PIX** | Abre tela com **QR** e opções de copiar código / gerar novo QR se expirar. |
| **Cartão** | Fluxo de confirmação com valor (em ambiente real, depende de integração; em testes é simulado). |
| **Dinheiro** | Pede confirmação **“Confirmar recebimento em dinheiro neste valor?”** — ao confirmar, registra no sistema. |

**Sem internet:** em geral **não** é possível concluir pagamento online; aparece **“Pagamento online indisponível offline. Reconecte-se.”** (regra da versão atual).

**Sucesso comum:** **“Pagamento confirmado.”** e retorno à lista.

---

## 8. Motorista (conta própria — horas em qualquer estacionamento)

Este perfil é **só para quem estaciona o carro**: cria a **própria conta** com **e-mail**, **senha** e **placa**. As **horas compradas** ficam numa **carteira global**; ao sair de um estacionamento, se o **ticket** for da **mesma placa**, o sistema pode **usar** essas horas automaticamente (além das regras do local). O motorista **não** usa as áreas de **operador**, **gestor** nem **super admin**.

### Ordem sugerida

1. **Criar conta** (primeira vez): no **login**, escolha **cadastro motorista**; informe e-mail, senha (mínimo 8 caracteres) e placa do veículo. Depois **entre** com o mesmo e-mail e senha.  
2. **Carteira:** vê **saldo de horas**, **placa** associada e **validade**, se houver.  
3. **Comprar horas:** lista de pacotes → **Selecionar** → **Crédito** (compra simulada interna) ou **PIX**.  
   - Crédito: confirma o aviso **“Confirmar compra a crédito interno? O valor será registrado.”**  
   - Sucesso: **“Compra concluída.”**  
4. **Histórico:** movimentos da carteira global (compras, usos no estacionamento, etc.), com “carregar mais” se existir.

**PIX na compra:** igual à ideia da tela de PIX do operador (QR, espera do pagamento, **Gerar novo QR** se expirar).

### Caminhos Web (referência)

- Cadastro: `/motorista/cadastro`  
- Carteira: `/motorista` (o endereço antigo `/cliente` redireciona para aqui)  
- Histórico: `/motorista/historico`  
- Comprar: `/motorista/comprar`  
- PIX: `/motorista/pix/:id` (aberto pelo fluxo de compra)

**Android:** no **login**, use **Cadastro motorista**; após entrar, a app abre a **carteira global** (mesma lógica da Web).

---

## 9. Lojista (convênio)

### Criar conta (cadastro)

1. Obtenha com a **gestão do estacionamento** o **código de 10 letras e números** do local (aparece ao criar o estacionamento no painel super) ou, em último caso, o **UUID** técnico — o mesmo critério que operadores usam para identificar o estacionamento.  
2. Na Web, abra **`/lojista/cadastro`** (ou no app Android, no **login**, **Cadastro lojista (convênio)**).  
3. Preencha **código ou UUID do estacionamento**, **nome do convênio** (loja), **e-mail** e **senha**.  
4. Envio bem-sucedido: volte ao **login** com o **mesmo e-mail** escolhido.

**Mesmo e-mail que motorista global:** se você já tiver conta de **motorista** (portal do condutor) com esse e-mail, o sistema pode **vincular** a conta do convênio à mesma identidade, mantendo uma senha coerente com o que definiu no cadastro lojista — use a **mesma senha** que desejar para os dois fluxos, conforme orientação da sua empresa.

**Após entrar:** perfis **CLIENT** com convênio precisam que o aplicativo envie o estacionamento correto nas operações da carteira convênio (a app faz isso automaticamente a partir do seu acesso). Rotas da carteira: **`/lojista`**, **`/lojista/historico`**, **`/lojista/comprar`**, **`/lojista/pix/...`** — fluxo semelhante ao **motorista**, com textos de **convênio**.

---

### Uso diário

Fluxo semelhante ao **motorista**: **saldo de horas** da loja, **histórico**, **compra** de créditos para beneficiar veículos (regras de valores e horas personalizáveis conforme política do estacionamento — ver especificação técnica do projeto).

---

## 10. Pagamento PIX — o que esperar

1. Abre-se a tela com **QR** (imagem) gerada a partir do código recebido do servidor.  
2. O cliente paga no app do banco **escaneando o QR** ou usando o **copia e cola** (**Copiar código PIX** → **“Código copiado.”**).  
3. O sistema **consulta o pagamento automaticamente** em intervalos curtos até **confirmar**, **expirar** ou **falhar**.  
4. Se o QR **expirar:** **“QR expirado.”** — use **Gerar novo QR**.  
5. Após muito tempo na mesma tela, pode aparecer limite de espera: **“Tempo limite de espera do pagamento. Use ‘Gerar novo QR’.”**  
6. **Pagamento falhou:** **“Pagamento falhou. Escolha outro método ou tente novamente.”** — volte à escolha de método.

**Ambiente de teste (desenvolvimento):** o “banco” pode ser simulado; quem mantém o servidor confirma o pagamento com **webhook** técnico. Em **produção com banco real**, o provedor PIX envia a confirmação automaticamente — o operador só precisa de **internet estável** na hora.

---

## 11. Sem internet (operador)

- Aparece aviso: **“Sem conexão. Algumas ações ficam bloqueadas.”**  
- **Nova entrada** pode ficar **desabilitada** ou enfileirada conforme a versão: se enfileirar, ao voltar a rede o sistema tenta enviar.  
- Se algo **não sincronizar** após várias tentativas: **“Não foi possível sincronizar uma operação. Verifique na lista de tickets.”**

**Recomendação:** sempre que possível, **reconecte** antes de **checkout** e **pagamento**.

---

## 12. Mensagens que você pode ver (e o que fazer)

| Mensagem | O que fazer |
|----------|-------------|
| Preencha este campo | Complete e-mail ou senha (ou outro campo indicado). |
| Operador bloqueado… | Fale com o **gestor**; desbloqueio é **fora do app** nesta versão. |
| Aguarde antes de tentar… | Espere alguns minutos antes de novo login. |
| Formato de placa inválido | Corrija a placa (Mercosul ou antiga). |
| Já existe ticket em aberto… | Use o ticket existente ou resolva com supervisão. |
| Não foi possível registrar a saída… | Atualize a tela; se persistir, suporte. |
| Pagamento falhou… | Tente outro método ou novo PIX. |
| Valor enviado não confere… | Na tela de cartão, confira o valor exibido pelo sistema. |
| Abra o caixa para habilitar dinheiro | Peça ao **gestor** para **abrir o caixa**. |
| Acesso negado | Use **Voltar ao início**; você não tem perfil para aquela área. |
| Escolha um estacionamento na lista | No site, super admin: abra a lista no topo e selecione um local antes de **Gestão**/**Operação**. |
| Nome do estacionamento: entre 2 e 120 caracteres | Ajuste o nome (nem vazio nem demasiado longo). |
| (mensagem de conflito do servidor sobre nome) | Escolha **outro nome** para o estacionamento — cada um deve ser **único**. |
| Dados podem estar desatualizados (offline) | Reconecte e atualize a lista. |

---

## 13. O que **não** se faz pelo aplicativo ou site

| Necessidade | Onde costuma ser feito |
|-------------|-------------------------|
| **Criar** um estacionamento novo | **Site Web** — **Criar estacionamento** em `/admin/tenant`; ou API / Postman. **Android:** não pela app. |
| **Desbloquear** operador suspenso | API / processo administrativo — **não** há botão no app na v1. |
| **Cadastrar ou editar pacotes** pela interface | Não previsto; dados vêm de banco/seed/processo técnico. |
| **Impressão fiscal / maquininha física / cancela automática** | Fora do escopo deste software conforme especificação do projeto. |

---

## 14. Referência rápida — telas Web

| Endereço (após o domínio do site) | Uso |
|-----------------------------------|-----|
| `/login` | Entrar |
| `/operador` | Início operador |
| `/operador/entrada` | Nova entrada |
| `/operador/ticket/:id` | Detalhe do ticket |
| `/operador/checkout/:ticketId` | Checkout |
| `/operador/pagar/:paymentId` | Escolher pagamento |
| `/operador/pix/:paymentId` | PIX |
| `/operador/cartao/:paymentId` | Cartão |
| `/gestor` | Painel |
| `/gestor/visao` | Visão estratégica (análise e extrato por período) |
| `/gestor/movimentos` | Extrato de movimentações (gestão) |
| `/gestor/caixa` | Caixa |
| `/gestor/config` | Configurações |
| `/motorista`, `/motorista/cadastro`, `/motorista/historico`, `/motorista/comprar` | Motorista (carteira global) |
| `/cliente`, … | Redireciona para `/motorista`, … |
| `/lojista/cadastro` | Cadastro de conta lojista (convênio) — código de 10 caracteres ou UUID do estacionamento |
| `/lojista`, … | Carteira e fluxos do lojista |
| `/admin/tenant` | Super admin — lista de estacionamentos (por nome único) + criar novo |
| `/proibido` | Acesso negado |

---

## 15. Dicas finais

1. **Guarde** o **UUID** de cada estacionamento em local seguro (só para quem precisa).  
2. **Abra o caixa** antes de esperar **dinheiro** nas cobranças.  
3. Em dúvida no fluxo de saída, siga sempre: **lista → ticket → checkout → pagamento → confirmado**.  
4. Problemas que não somem com **atualizar** ou **sair e entrar de novo**: abra chamado com **print da mensagem** e **horário** — ajuda o suporte técnico.

---

*Manual alinhado à interface descrita em `SPEC_FRONTEND.md` v1.13. Ajuste datas, nomes de domínio e procedimentos internos da sua empresa neste documento se necessário.*
