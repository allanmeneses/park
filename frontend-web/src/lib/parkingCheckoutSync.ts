import type { AxiosInstance } from 'axios'
import axios from 'axios'

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

/**
 * Em AWAITING_PAYMENT, o recalculo pode devolver conflito de estado em corridas de sincronização.
 * Nesses casos, o fluxo de "Pagar" deve continuar usando o estado mais recente carregado em seguida.
 */
export function canIgnoreCheckoutRefreshError(err: unknown): boolean {
  if (!axios.isAxiosError(err)) return false
  if (err.response?.status !== 409) return false
  const data = err.response?.data as Record<string, unknown> | undefined
  const code = String(data?.code ?? '').toUpperCase()
  return code === 'INVALID_TICKET_STATE' || code === 'CONFLICT'
}

/**
 * Mantém o valor do pagamento alinhado ao tempo no pátio (tickets): recalcula checkout e devolve o amount atual.
 * Pacotes (sem ticket_id) devolve amount do GET sem POST.
 */
export async function refreshTicketPaymentAmountForPixSync(
  api: AxiosInstance,
  paymentId: string,
): Promise<{ amount: string; ticketId: string | null }> {
  const { data: pay0 } = await api.get<Record<string, unknown>>(`/payments/${paymentId}`)
  const tid = ticketIdFromPaymentPayload(pay0)
  if (!tid) {
    return { amount: String(pay0.amount ?? pay0.Amount ?? ''), ticketId: null }
  }
  const st = String(pay0.status ?? pay0.Status ?? '').toUpperCase()
  if (st === 'PENDING' || st === 'EXPIRED' || st === 'FAILED') {
    try {
      await refreshPendingCheckoutForTicket(api, tid)
    } catch (e: unknown) {
      if (!(axios.isAxiosError(e) && e.response?.status === 409 && canIgnoreCheckoutRefreshError(e))) {
        throw e
      }
    }
  }
  const { data: pay1 } = await api.get<Record<string, unknown>>(`/payments/${paymentId}`)
  return { amount: String(pay1.amount ?? pay1.Amount ?? ''), ticketId: tid }
}
