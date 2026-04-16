package com.estacionamento.parking.ui

/** SPEC_FRONTEND §9 — literais usados no Android. */
object UiStrings {
    const val B1 = "Entrar"
    const val B24 = "Criar conta"
    const val B34 = "Cadastro de cliente"
    /** Link no login — cadastro lojista. */
    const val B25 = "Cadastro de lojista"
    const val B2 = "Nova entrada"
    const val B3 = "Registrar problema"
    const val B4 = "Registrar saída (checkout)"
    const val B5 = "Pagar"
    /** Operador — recálculo antes de abrir pagamento (ticket aguardando pagamento). */
    const val B31 = "A atualizar…"
    const val B6 = "PIX"
    const val B7 = "Cartão"
    const val B8 = "Dinheiro"
    const val B9 = "Copiar código PIX"
    const val B10 = "Gerar novo QR"
    const val B11 = "Voltar ao início"
    const val B12 = "Caixa"
    const val B13 = "Configurações"
    const val B14 = "Abrir caixa"
    const val B15 = "Fechar caixa"
    const val B16 = "Comprar horas"
    const val B17 = "Histórico"
    const val B18 = "Selecionar"
    const val B20 = "Gestão"
    const val B21 = "Operação"
    /** SPEC_FRONTEND §5.10 — extrato / insights de movimentações. */
    const val B22 = "Insights"
    /** SPEC_FRONTEND §5.10.2 — tendências e horários de pico. */
    const val B23 = "Análises"
    /** SPEC_FRONTEND §5.10.3 — saldos convênio e carteira comprada por placa. */
    const val B32 = "Relatório de saldos"
    /** Painel gestor — convites / cadastro de lojistas (ADMIN, SUPER_ADMIN). */
    const val B26 = "Cadastro de lojistas"
    /** Lojista — bonificar horas na carteira do cliente. */
    const val B27 = "Bonificar cliente"
    /** Lojista — leitor QR do cupom (ticket). */
    const val B28 = "Escanear QR do cupom"
    /** Lojista — extrato só de bonificações concedidas. */
    const val B29 = "Extrato de bonificações"
    /** Lojista — interruptor: só bonificar com veículo no pátio. */
    const val B30 = "Só bonificar com veículo no pátio"
    const val D1 = "Confirmar recebimento em dinheiro neste valor?"
    const val D2 = "Confirmar compra a crédito interno? O valor será registrado."
    const val S1 = "Nenhum veículo no pátio."
    const val S2 = "Sem conexão. Algumas ações ficam bloqueadas."
    const val S3 = "Dados podem estar desatualizados (offline)."
    const val S4 = "Ticket encerrado."
    const val S5 = "Abra o caixa para habilitar dinheiro."
    const val S6 = "Pagamento online indisponível offline. Reconecte-se."
    const val S7 = "QR expirado."
    const val S8 = "Tempo limite de espera do pagamento. Use “Gerar novo QR”."
    const val S9 = "Acesso negado"
    const val S10 = "Você não pode abrir esta área com seu perfil."
    const val S11 = "Sessão de caixa"
    const val S12 = "Nenhum pacote cadastrado para este tipo."
    const val S15 = "Informe o ID do estacionamento (UUID) para continuar."
    const val S16 = "UUID inválido."
    const val S29 = "Pagamento com cartão processado pelo Mercado Pago em formulário embutido."
    const val S30 = "Forma de pagamento"
    const val S37 = "Pagamento com cartão disponível apenas para valores a partir de R$ 1,00. Para este pacote, use PIX."
    const val S31 = "Carregar mais"
    const val S32 = "Carregando..."
    const val S33 = "Hoje (UTC)"
    const val S34 = "Últimas 24h"
    const val S35 = "Últimos 7 dias"
    const val S36 = "Últimos 30 dias"
    const val S17 = "Desligado: você pode bonificar só com a placa, antes da entrada no estacionamento."
    const val S18 = "Ligado: bonificação só com veículo no pátio (ticket em aberto ou aguardando pagamento), ou pelo QR do cupom."
    const val S19 = "Modo restrito: bonificação exige veículo no estacionamento (entrada registrada) ou use o código do cupom."
    /** Online: data/hora do dispositivo fora do tolerado vs servidor (GET /health). */
    const val S25 =
        "Data e hora do dispositivo estão incorretas. Ajuste a data (deve coincidir com a de referência) e a hora (margem de 5 minutos) nas configurações do sistema. Sem isso o aplicativo fica bloqueado enquanto houver internet."
    /** Cartão — checkout PSP (SPEC_FRONTEND §5.8). */
    const val S27 =
        "Complete o pagamento na página que abriu. Esta tela verifica automaticamente quando o pagamento for confirmado."
    /** Cartão — polling sem confirmação (hosted checkout). */
    const val S28 =
        "Ainda não há confirmação do pagamento. Abra o link de novo ou tente outro método."
    /** Cartão — reabrir checkout. */
    const val B33 = "Abrir pagamento no site"
    /** Gestor — Mercado Pago por tenant (PSP). */
    const val B37 = "Mercado Pago (PSP)"
    const val B35 = "Pagar com PIX"
    const val B36 = "Pagar com cartão"
    /** Detalhe do ticket — cabeçalho da lista de convênios (GET /tickets/{id} lojistaBenefits). */
    const val S22 = "Convênios (lojistas)"
    /** Item da lista: sufixo após horas disponíveis. */
    const val S23 = "h disponíveis na saída"
    /** Horas concedidas no total (quando difere do disponível). */
    const val S24 = "h concedidas no total"
    /** Detalhe do ticket — ordem de consumo na saída (convênio antes da carteira comprada). */
    const val S26 =
        "Na saída: primeiro saldo bonificado do convênio, depois carteira comprada; só então valor a pagar."
    const val T1 = "Problema registrado."
    const val T2 = "Entrada registrada."
    const val T3 = "Saída registrada. Nada a pagar."
    const val T4 = "Pagamento confirmado."
    const val T5 = "Código copiado."
    const val T6 = "Alerta: divergência no caixa acima do limite."
    const val T7 = "Configurações salvas."
    /** PSP Mercado Pago por tenant gravado. */
    const val T11 = "Configuração PSP guardada."
    const val T8 = "Compra concluída."
    /** Bonificação ao cliente gravada com sucesso. */
    const val T10 = "Bonificação registrada."
    const val T9 = "Não foi possível sincronizar uma operação. Verifique na lista de tickets."
    /** SPEC §5.3 offline — fila §10 (texto operacional alinhado ao fluxo). */
    const val TQueueSync = "Sem rede: operação na fila e será enviada ao reconectar."
    const val E1 = "Operador bloqueado. Procure o gestor."
    const val E2 = "Aguarde antes de tentar de novo."
    const val E3 = "Preencha este campo."
    const val E9 = "O código do lojista deve ter 10 caracteres."
    const val E4 = "Formato de placa inválido."
    const val E5 = "Já existe ticket em aberto para esta placa."
    const val E6 = "Não foi possível registrar a saída neste estado."
    const val E7 = "Pagamento falhou. Escolha outro método ou tente novamente."
    const val E8 = "Valor enviado não confere com o ticket."
    const val Sair = "Sair"
    const val Definir = "Definir"
    const val Confirmar = "Confirmar"
    const val Voltar = "Voltar"
    const val Continuar = "Continuar"
    const val Placa = "Placa do veículo"
    /** Campo UUID do estacionamento (SUPER_ADMIN) — SPEC_FRONTEND §5.18 / §11. */
    const val FieldParkingUuid = "UUID do estacionamento"
    const val Salvar = "Salvar"
    const val Credito = "Crédito"
    const val Pix = "PIX"

    /** Resumo de saldos após `POST /lojista/grant-client` (bonificado da placa vs carteira do lojista). */
    fun grantSaldoBonificadoResumo(clientBonificadoHoras: Int, lojistaSaldoHoras: Int): String =
        "Saldo bonificado da placa: $clientBonificadoHoras h. Seu saldo: $lojistaSaldoHoras h."
}
