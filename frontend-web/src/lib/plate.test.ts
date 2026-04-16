import { describe, expect, it } from 'vitest'
import {
  PLATE_DISPLAY_MAX_LENGTH,
  formatPlateDisplay,
  isValidPlate,
  normalizePlate,
  plateDisplayIndexToRawLength,
  plateRawLengthToDisplayIndex,
  sanitizePlateInput,
} from './plate'

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

  it('sanitizePlateInput keeps letters in prefix and valid Mercosul/legado tail', () => {
    expect(sanitizePlateInput('ab*12')).toBe('AB')
    expect(sanitizePlateInput('abc*12')).toBe('ABC12')
    expect(sanitizePlateInput('abc-1234')).toBe('ABC1234')
    expect(sanitizePlateInput('abc1d23')).toBe('ABC1D23')
    expect(sanitizePlateInput('1abc1234')).toBe('ABC1234')
    expect(sanitizePlateInput('abcd1234')).toBe('ABC1234')
  })

  it('formatPlateDisplay inserts hyphen after third letter', () => {
    expect(formatPlateDisplay('AB')).toBe('AB')
    expect(formatPlateDisplay('ABC')).toBe('ABC')
    expect(formatPlateDisplay('ABC1')).toBe('ABC-1')
    expect(formatPlateDisplay('ABC1234')).toBe('ABC-1234')
    expect(formatPlateDisplay('ABC1D23')).toBe('ABC-1D23')
  })

  it('PLATE_DISPLAY_MAX_LENGTH matches longest formatted plate', () => {
    expect(PLATE_DISPLAY_MAX_LENGTH).toBe(8)
    expect(formatPlateDisplay('ABC1234').length).toBe(PLATE_DISPLAY_MAX_LENGTH)
  })

  it('maps display cursor to raw length and back', () => {
    expect(plateDisplayIndexToRawLength(0, 'ABC-1')).toBe(0)
    expect(plateDisplayIndexToRawLength(4, 'ABC-1')).toBe(3)
    expect(plateDisplayIndexToRawLength(8, 'ABC-1234')).toBe(7)
    expect(plateRawLengthToDisplayIndex(0)).toBe(0)
    expect(plateRawLengthToDisplayIndex(3)).toBe(3)
    expect(plateRawLengthToDisplayIndex(4)).toBe(5)
    expect(plateRawLengthToDisplayIndex(7)).toBe(8)
  })
})
