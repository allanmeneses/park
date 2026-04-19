import { describe, expect, it, vi } from 'vitest'
import type { AxiosInstance } from 'axios'
import {
  canIgnoreCheckoutRefreshError,
  refreshPendingCheckoutForTicket,
  refreshTicketPaymentAmountForPixSync,
  ticketIdFromPaymentPayload,
} from './parkingCheckoutSync'

describe('ticketIdFromPaymentPayload', () => {
  it('lê ticket_id', () => {
    expect(ticketIdFromPaymentPayload({ ticket_id: 'abc' })).toBe('abc')
  })
  it('lê ticketId camelCase', () => {
    expect(ticketIdFromPaymentPayload({ ticketId: 'def' })).toBe('def')
  })
  it('prefere snake quando ambos', () => {
    expect(ticketIdFromPaymentPayload({ ticket_id: 'a', ticketId: 'b' })).toBe('a')
  })
  it('null sem ticket', () => {
    expect(ticketIdFromPaymentPayload({ amount: '1' })).toBeNull()
  })
})

describe('refreshPendingCheckoutForTicket', () => {
  it('POST checkout com Idempotency-Key UUID', async () => {
    const post = vi.fn().mockResolvedValue({ data: {} })
    const api = { post } as unknown as AxiosInstance
    await refreshPendingCheckoutForTicket(api, 'tid-1')
    expect(post).toHaveBeenCalledTimes(1)
    expect(post).toHaveBeenCalledWith(
      '/tickets/tid-1/checkout',
      {},
      expect.objectContaining({
        headers: expect.objectContaining({
          'Idempotency-Key': expect.stringMatching(
            /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
          ),
        }),
      }),
    )
  })
})

describe('refreshTicketPaymentAmountForPixSync', () => {
  it('sem ticket_id só faz GET', async () => {
    const get = vi
      .fn()
      .mockResolvedValueOnce({ data: { amount: '10.00', package_order_id: 'po-1' } })
    const api = { get } as unknown as AxiosInstance
    const r = await refreshTicketPaymentAmountForPixSync(api, 'pay-1')
    expect(get).toHaveBeenCalledTimes(1)
    expect(r.ticketId).toBeNull()
    expect(r.amount).toBe('10.00')
  })

  it('com ticket PENDING chama checkout e relê pagamento', async () => {
    const get = vi
      .fn()
      .mockResolvedValueOnce({
        data: { amount: '5.00', ticket_id: 't1', status: 'PENDING' },
      })
      .mockResolvedValueOnce({ data: { amount: '15.00', ticket_id: 't1', status: 'PENDING' } })
    const post = vi.fn().mockResolvedValue({ data: {} })
    const api = { get, post } as unknown as AxiosInstance
    const r = await refreshTicketPaymentAmountForPixSync(api, 'pay-1')
    expect(post).toHaveBeenCalledTimes(1)
    expect(get).toHaveBeenCalledTimes(2)
    expect(r.amount).toBe('15.00')
    expect(r.ticketId).toBe('t1')
  })
})

describe('canIgnoreCheckoutRefreshError', () => {
  it('true para 409 INVALID_TICKET_STATE', () => {
    const err = {
      isAxiosError: true,
      response: { status: 409, data: { code: 'INVALID_TICKET_STATE' } },
    }
    expect(canIgnoreCheckoutRefreshError(err)).toBe(true)
  })

  it('false para outros erros', () => {
    const err = {
      isAxiosError: true,
      response: { status: 400, data: { code: 'VALIDATION_ERROR' } },
    }
    expect(canIgnoreCheckoutRefreshError(err)).toBe(false)
    expect(canIgnoreCheckoutRefreshError(new Error('x'))).toBe(false)
  })
})
