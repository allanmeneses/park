/** SPEC_FRONTEND §9 — literais fixos (subset usado na v1). */

export const STRINGS = {
  B1: 'Entrar',
  B24: 'Criar conta',
  B2: 'Nova entrada',
  B11: 'Voltar ao início',
  B20: 'Gestão',
  B21: 'Operação',
  B26: 'Cadastro de lojistas',
  /** Gestão — relatório de saldos (lojista + cliente por placa). */
  B32: 'Relatório de saldos',
  E3: 'Preencha este campo.',
  E9: 'O código do lojista deve ter 10 caracteres.',
  S1: 'Nenhum veículo no pátio.',
  S2: 'Sem conexão. Algumas ações ficam bloqueadas.',
  S9: 'Acesso negado',
  S10: 'Você não pode abrir esta área com seu perfil.',
  S15: 'Informe o ID do estacionamento (UUID) para continuar.',
  B30: 'Só bonificar com veículo no pátio',
  S17: 'Desligado: você pode bonificar só com a placa, antes da entrada no estacionamento.',
  S18: 'Ligado: bonificação só com veículo no pátio (ticket em aberto ou aguardando pagamento), ou pelo QR do cupom.',
  S19: 'Modo restrito: bonificação exige veículo no estacionamento (entrada registrada) ou use o código do cupom.',
  /** Online: data/hora do dispositivo fora do tolerado vs servidor (GET /health). */
  S25:
    'Data e hora do dispositivo estão incorretas. Ajuste a data (deve coincidir com a de referência) e a hora (margem de 5 minutos) nas configurações do sistema. Sem isso o aplicativo fica bloqueado enquanto houver internet.',
} as const
