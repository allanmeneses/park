import { describe, expect, it } from 'vitest'
import { isZeroMoneyAmount } from './moneyParse'

describe('isZeroMoneyAmount', () => {
  it('aceita "0.00" e variantes', () => {
    expect(isZeroMoneyAmount('0.00')).toBe(true)
    expect(isZeroMoneyAmount('0')).toBe(true)
    expect(isZeroMoneyAmount('0,00')).toBe(true)
    expect(isZeroMoneyAmount(0)).toBe(true)
  })
  it('não-zero', () => {
    expect(isZeroMoneyAmount('5.00')).toBe(false)
    expect(isZeroMoneyAmount(5)).toBe(false)
  })
})
