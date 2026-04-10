import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { str } from '@/lib/apiDto'
import { apiErrorMessage } from '@/lib/errors'
import { normalizePaymentStatus } from '@/lib/paymentStatus'

export type PixPollOnceResult =
  | { kind: 'paid' }
  | { kind: 'expired' }
  | { kind: 'failed' }
  | { kind: 'pending' }
  | { kind: 'error'; message: string; unauthorized: boolean }

/** Uma leitura de GET /payments/:id para ecrãs de QR PIX (polling + foco). */
export async function pollPaymentOnce(api: AxiosInstance, paymentId: string): Promise<PixPollOnceResult> {
  try {
    const { data } = await api.get<Record<string, unknown>>(`/payments/${paymentId}`)
    const st = normalizePaymentStatus(data.status ?? data.Status)
    if (st === 'PAID') return { kind: 'paid' }
    if (st === 'EXPIRED') return { kind: 'expired' }
    if (st === 'FAILED') return { kind: 'failed' }
    return { kind: 'pending' }
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) {
      const code = e.response?.status
      if (code === 401 || code === 403) {
        return {
          kind: 'error',
          message: 'Sessão expirou ou sem permissão. Entre de novo e volte a esta página, ou use Gerar novo QR.',
          unauthorized: true,
        }
      }
      const msg = apiErrorMessage(e.response?.data)
      return {
        kind: 'error',
        message: msg || str(e.message) || 'Erro ao verificar pagamento.',
        unauthorized: false,
      }
    }
    return { kind: 'error', message: 'Erro ao verificar pagamento.', unauthorized: false }
  }
}
