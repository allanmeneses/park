import { describe, expect, it } from 'vitest'
import {
  formatHistoryAmountBrl,
  formatHistoryDeltaHours,
  formatHistoryWhen,
  historyKindLabel,
  walletHistoryItemFromApi,
} from './walletHistory'

describe('walletHistory', () => {
  it('historyKindLabel', () => {
    expect(historyKindLabel('PURCHASE')).toBe('Compra')
    expect(historyKindLabel('USAGE')).toBe('Uso')
  })

  it('formatHistoryDeltaHours', () => {
    expect(formatHistoryDeltaHours('PURCHASE', 5)).toBe('+5 h')
    expect(formatHistoryDeltaHours('USAGE', -2)).toBe('-2 h')
    expect(formatHistoryDeltaHours('USAGE', 3)).toBe('-3 h')
  })

  it('walletHistoryItemFromApi aceita snake_case', () => {
    const it = walletHistoryItemFromApi({
      id: 'a',
      kind: 'PURCHASE',
      delta_hours: 10,
      amount: '50.00',
      created_at: '2026-01-01T12:00:00Z',
    })
    expect(it.deltaHours).toBe(10)
    expect(it.amount).toBe('50.00')
    expect(it.createdAt).toBe('2026-01-01T12:00:00Z')
  })

  it('formatHistoryAmountBrl', () => {
    expect(formatHistoryAmountBrl('50.00')).toMatch(/50/)
    expect(formatHistoryAmountBrl('50,00')).toMatch(/50/)
  })

  it('formatHistoryWhen não quebra com ISO', () => {
    const s = formatHistoryWhen('2026-03-27T15:30:00.000Z')
    expect(s.length).toBeGreaterThan(5)
  })
})
