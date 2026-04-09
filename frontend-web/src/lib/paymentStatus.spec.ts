import { describe, expect, it } from 'vitest'
import { isPaidStatus, normalizePaymentStatus } from './paymentStatus'

describe('paymentStatus helpers', () => {
  it('normalizes to uppercase without spaces', () => {
    expect(normalizePaymentStatus(' paid ')).toBe('PAID')
  })

  it('accepts PAID in any case', () => {
    expect(isPaidStatus('PAID')).toBe(true)
    expect(isPaidStatus('paid')).toBe(true)
    expect(isPaidStatus('Paid')).toBe(true)
  })

  it('rejects non-paid statuses', () => {
    expect(isPaidStatus('PENDING')).toBe(false)
    expect(isPaidStatus('EXPIRED')).toBe(false)
  })
})
