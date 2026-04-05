import { describe, expect, it, vi } from 'vitest'
import type { AxiosInstance } from 'axios'
import { refreshPendingCheckoutForTicket, ticketIdFromPaymentPayload } from './parkingCheckoutSync'

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
