import { describe, expect, it } from 'vitest'
import { isValidPlate, normalizePlate } from './plate'

describe('plate', () => {
  it('normalizes', () => {
    expect(normalizePlate(' abc-1d23 ')).toBe('ABC1D23')
  })

  it('accepts Mercosul', () => {
    expect(isValidPlate('ABC1D23')).toBe(true)
  })

  it('accepts legado', () => {
    expect(isValidPlate('ABC1234')).toBe(true)
  })

  it('rejects invalid', () => {
    expect(isValidPlate('AB12345')).toBe(false)
  })
})
