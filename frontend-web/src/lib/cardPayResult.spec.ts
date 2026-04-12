import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import {
  interpretCardPayResponse,
  mapPaymentStatus,
  pollPaymentUntilTerminal,
} from './cardPayResult'

describe('interpretCardPayResponse', () => {
  it('sync_paid quando status PAID sem mode', () => {
    const r = interpretCardPayResponse({ payment_id: 'x', status: 'PAID', provider: 'stub' }, false)
    expect(r).toEqual({ kind: 'sync_paid', status: 'PAID' })
  })

  it('embedded_bricks expõe provider e public key', () => {
    const r = interpretCardPayResponse(
      {
        mode: 'embedded_bricks',
        provider: 'mercadopago',
        public_key: 'pk_test',
      },
      false,
    )
    expect(r).toEqual({
      kind: 'embedded_bricks',
      provider: 'mercadopago',
      publicKey: 'pk_test',
    })
  })

  it('hosted_checkout usa init_point em produção', () => {
    const r = interpretCardPayResponse(
      {
        mode: 'hosted_checkout',
        init_point: 'https://mp/init',
        sandbox_init_point: 'https://mp/sandbox',
        preference_id: 'pref-1',
      },
      false,
    )
    expect(r).toMatchObject({
      kind: 'hosted_checkout',
      openUrl: 'https://mp/init',
      preferenceId: 'pref-1',
    })
  })

  it('hosted_checkout prefere sandbox quando useSandboxUrl', () => {
    const r = interpretCardPayResponse(
      {
        mode: 'hosted_checkout',
        init_point: 'https://mp/init',
        sandbox_init_point: 'https://mp/sandbox',
      },
      true,
    )
    expect(r).toMatchObject({ kind: 'hosted_checkout', openUrl: 'https://mp/sandbox' })
  })

  it('unknown sem URLs', () => {
    expect(interpretCardPayResponse({ mode: 'hosted_checkout' }, false).kind).toBe('unknown')
  })

  it('pending_status preserva status do PSP', () => {
    const r = interpretCardPayResponse(
      {
        status: 'PENDING',
        provider_status: 'in_process',
        provider_status_detail: 'pending_review_manual',
      },
      false,
    )
    expect(r).toEqual({
      kind: 'pending_status',
      status: 'PENDING',
      providerStatus: 'in_process',
      providerStatusDetail: 'pending_review_manual',
    })
  })

  it('failed_status usa o detalhe do PSP como mensagem', () => {
    const r = interpretCardPayResponse(
      {
        status: 'FAILED',
        provider_status: 'rejected',
        provider_status_detail: 'cc_rejected_bad_filled_card_number',
      },
      false,
    )
    expect(r).toEqual({
      kind: 'failed_status',
      status: 'FAILED',
      message: 'cc_rejected_bad_filled_card_number',
      providerStatus: 'rejected',
      providerStatusDetail: 'cc_rejected_bad_filled_card_number',
    })
  })
})

describe('mapPaymentStatus', () => {
  it('mapeia estados', () => {
    expect(mapPaymentStatus('PAID')).toBe('paid')
    expect(mapPaymentStatus('failed')).toBe('failed')
    expect(mapPaymentStatus('PENDING')).toBe('pending')
  })
})

describe('pollPaymentUntilTerminal', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('retorna paid na primeira leitura', async () => {
    const p = pollPaymentUntilTerminal(async () => 'PAID', {
      intervalMs: 2000,
      maxWaitMs: 60_000,
    })
    await vi.runAllTimersAsync()
    await expect(p).resolves.toBe('paid')
  })

  it('timeout mantém pending', async () => {
    const p = pollPaymentUntilTerminal(async () => 'PENDING', {
      intervalMs: 1000,
      maxWaitMs: 2500,
    })
    await vi.advanceTimersByTimeAsync(3000)
    await expect(p).resolves.toBe('pending')
  })
})
