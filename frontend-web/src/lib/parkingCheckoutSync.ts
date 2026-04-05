import type { AxiosInstance } from 'axios'

/**
 * Recalcula checkout em AWAITING_PAYMENT: sem exit_time, a API usa o instante atual (SPEC).
 */
export async function refreshPendingCheckoutForTicket(
  api: AxiosInstance,
  ticketId: string,
): Promise<void> {
  await api.post(`/tickets/${ticketId}/checkout`, {}, {
    headers: { 'Idempotency-Key': crypto.randomUUID() },
  })
}

/**
 * Identificador do ticket em respostas GET /payments/:id (snake ou camel).
 * Usado antes de POST /tickets/:id/checkout para atualizar saída em pagamento pendente.
 */
export function ticketIdFromPaymentPayload(raw: Record<string, unknown>): string | null {
  const v = raw.ticket_id ?? raw.ticketId
  if (v == null || typeof v !== 'string') return null
  const s = v.trim()
  return s.length ? s : null
}
