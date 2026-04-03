/** SPEC_FRONTEND §8 — fallback quando `message` vazio. */

const MAP: Record<string, string> = {
  VALIDATION_ERROR: 'Verifique os dados informados.',
  UNAUTHORIZED: 'Sessão expirada. Faça login novamente.',
  FORBIDDEN: 'Você não tem permissão.',
  NOT_FOUND: 'Registro não encontrado.',
  CONFLICT: 'Operação não permitida no estado atual.',
  PLATE_INVALID: 'Placa inválida.',
  PLATE_HAS_ACTIVE_TICKET: 'Já existe ticket aberto para esta placa.',
  INVALID_TICKET_STATE: 'Ticket não está nesta etapa.',
  LOJISTA_WALLET_MISSING:
    'Convênio indisponível: carteira do lojista não configurada.',
  PAYMENT_ALREADY_PAID: 'Pagamento já confirmado.',
  AMOUNT_MISMATCH: 'Valor não confere.',
  CASH_SESSION_REQUIRED: 'Abra o caixa antes de receber em dinheiro.',
  OPERATOR_BLOCKED: 'Operador bloqueado. Procure o gestor.',
  TENANT_UNAVAILABLE: 'Estacionamento indisponível. Tente mais tarde.',
  LOGIN_THROTTED: 'Muitas tentativas. Aguarde e tente novamente.',
  CLOCK_SKEW: 'Relógio do aparelho incorreto. Ajuste a data e hora.',
  INTERNAL: 'Erro no servidor. Tente novamente.',
  LOJISTA_INVITE_INVALID: 'Código do lojista ou ativação inválidos.',
  LOJISTA_INVITE_CONSUMED: 'Este convite já foi utilizado.',
  LOJISTA_CREDIT_INSUFFICIENT: 'Créditos insuficientes na sua carteira de convênio.',
  CLIENT_FOR_OTHER_LOJISTA: 'Esta placa está vinculada a outro convênio.',
  GRANT_REQUIRES_ACTIVE_TICKET:
    'É necessário ticket em aberto para esta placa, ou permita crédito antecipado na carteira.',
}

export function apiErrorMessage(body: unknown, fallback = 'Erro inesperado.'): string {
  if (body && typeof body === 'object' && 'message' in body) {
    const m = (body as { message?: unknown }).message
    if (typeof m === 'string' && m.trim()) return m
    const code = (body as { code?: unknown }).code
    if (typeof code === 'string' && MAP[code]) return MAP[code]!
  }
  return fallback
}
