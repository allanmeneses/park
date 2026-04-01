import { describe, expect, it } from 'vitest'
import { elapsedWholeSeconds, formatElapsedPtBr } from './elapsedRealtime'

describe('elapsedRealtime', () => {
  it('elapsedWholeSeconds arredonda para baixo e não negativiza', () => {
    const a = new Date('2026-03-31T10:00:00.000Z')
    const b = new Date('2026-03-31T10:00:45.900Z')
    expect(elapsedWholeSeconds(a, b)).toBe(45)
    expect(elapsedWholeSeconds(b, a)).toBe(0)
  })

  it('formatElapsedPtBr — horas', () => {
    expect(formatElapsedPtBr(3665)).toBe('1 h 1 min 5 s')
  })

  it('formatElapsedPtBr — sempre inclui h, min e s', () => {
    expect(formatElapsedPtBr(90)).toBe('0 h 1 min 30 s')
    expect(formatElapsedPtBr(0)).toBe('0 h 0 min 0 s')
    expect(formatElapsedPtBr(59)).toBe('0 h 0 min 59 s')
  })
})
