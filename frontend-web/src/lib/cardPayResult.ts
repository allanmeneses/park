/** Interpreta POST /payments/card — stub (síncrono) vs PSP checkout hospedado (ex.: Mercado Pago). */

export type CardPayInterpretation =
  | { kind: 'sync_paid'; status: string }
  | { kind: 'embedded_bricks'; provider: string | null; publicKey: string | null }
  | { kind: 'hosted_checkout'; openUrl: string; preferenceId?: string; publicKey?: string | null }
  | { kind: 'pending_status'; status: string; providerStatus?: string | null; providerStatusDetail?: string | null }
  | { kind: 'failed_status'; status: string; message: string; providerStatus?: string | null; providerStatusDetail?: string | null }
  | { kind: 'unknown' }

function str(v: unknown): string | undefined {
  return typeof v === 'string' ? v : undefined
}

/**
 * @param useSandboxUrl — em desenvolvimento, preferir `sandbox_init_point` quando existir (SPEC_FRONTEND §5.8).
 */
export function interpretCardPayResponse(data: unknown, useSandboxUrl: boolean): CardPayInterpretation {
  if (data === null || typeof data !== 'object') return { kind: 'unknown' }
  const o = data as Record<string, unknown>
  const mode = str(o.mode)
  if (mode === 'embedded_bricks') {
    return {
      kind: 'embedded_bricks',
      provider: str(o.provider) ?? null,
      publicKey: str(o.public_key) ?? null,
    }
  }
  if (mode === 'hosted_checkout') {
    const init = str(o.init_point)
    const sand = str(o.sandbox_init_point)
    const openUrl = useSandboxUrl && sand ? sand : init ?? sand ?? ''
    if (!openUrl) return { kind: 'unknown' }
    return {
      kind: 'hosted_checkout',
      openUrl,
      preferenceId: str(o.preference_id),
      publicKey: str(o.public_key) ?? null,
    }
  }
  const status = str(o.status)
  if (status?.toUpperCase() === 'PAID') return { kind: 'sync_paid', status }
  if (status?.toUpperCase() === 'PENDING') {
    return {
      kind: 'pending_status',
      status,
      providerStatus: str(o.provider_status) ?? null,
      providerStatusDetail: str(o.provider_status_detail) ?? null,
    }
  }
  if (status?.toUpperCase() === 'FAILED' || status?.toUpperCase() === 'EXPIRED') {
    const providerStatus = str(o.provider_status) ?? null
    const providerStatusDetail = str(o.provider_status_detail) ?? null
    const message = providerStatusDetail ?? providerStatus ?? status
    return {
      kind: 'failed_status',
      status,
      message,
      providerStatus,
      providerStatusDetail,
    }
  }
  return { kind: 'unknown' }
}

export type PaymentPollStatus = 'paid' | 'failed' | 'expired' | 'pending'

export function mapPaymentStatus(raw: string): PaymentPollStatus {
  const u = raw.toUpperCase()
  if (u === 'PAID') return 'paid'
  if (u === 'FAILED') return 'failed'
  if (u === 'EXPIRED') return 'expired'
  return 'pending'
}

/**
 * Polling até PAID / FAILED / EXPIRED ou timeout (SPEC_FRONTEND — mesmo espírito do Pix).
 */
export async function pollPaymentUntilTerminal(
  getStatus: () => Promise<string>,
  options: { intervalMs: number; maxWaitMs: number; signal?: AbortSignal },
): Promise<PaymentPollStatus> {
  const start = Date.now()
  while (Date.now() - start < options.maxWaitMs) {
    if (options.signal?.aborted) return 'pending'
    const s = mapPaymentStatus(await getStatus())
    if (s !== 'pending') return s
    await delay(options.intervalMs, options.signal)
  }
  return 'pending'
}

function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(new DOMException('Aborted', 'AbortError'))
      return
    }
    const t = setTimeout(resolve, ms)
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(t)
        reject(new DOMException('Aborted', 'AbortError'))
      },
      { once: true },
    )
  })
}
