import type { AxiosInstance } from 'axios'
import { describe, expect, it, vi } from 'vitest'
import { pollPaymentOnce } from './pixPaymentPoll'

function mockApi(getImpl: () => Promise<unknown>): AxiosInstance {
  return {
    get: vi.fn().mockImplementation(getImpl),
  } as unknown as AxiosInstance
}

describe('pollPaymentOnce', () => {
  it('paid quando status PAID', async () => {
    const api = mockApi(async () => ({ data: { status: 'PAID' } }))
    await expect(pollPaymentOnce(api, 'p1')).resolves.toEqual({ kind: 'paid' })
  })

  it('pending quando PENDING', async () => {
    const api = mockApi(async () => ({ data: { status: 'PENDING' } }))
    await expect(pollPaymentOnce(api, 'p1')).resolves.toEqual({ kind: 'pending' })
  })

  it('401 → error unauthorized', async () => {
    const api = mockApi(async () => {
      throw Object.assign(new Error('u'), {
        isAxiosError: true as const,
        response: { status: 401, data: {} },
      })
    })
    const r = await pollPaymentOnce(api, 'p1')
    expect(r.kind).toBe('error')
    if (r.kind === 'error') expect(r.unauthorized).toBe(true)
  })
})
