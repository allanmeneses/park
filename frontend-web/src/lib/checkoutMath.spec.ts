import { describe, expect, it } from 'vitest'
import { computeBillableHours } from './checkoutMath'

describe('checkoutMath', () => {
  it('ceil de horas parciais', () => {
    const e = new Date('2026-03-31T10:00:00.000Z')
    const x = new Date('2026-03-31T10:01:00.000Z')
    expect(computeBillableHours(e, x)).toBe(1)
  })

  it('zero quando exit antes ou igual a entry', () => {
    const e = new Date('2026-03-31T12:00:00.000Z')
    expect(computeBillableHours(e, e)).toBe(0)
    expect(computeBillableHours(e, new Date('2026-03-31T11:00:00.000Z'))).toBe(0)
  })

  it('hora cheia sem arredondar para cima desnecessariamente', () => {
    const e = new Date('2026-03-31T08:00:00.000Z')
    const x = new Date('2026-03-31T09:00:00.000Z')
    expect(computeBillableHours(e, x)).toBe(1)
  })
})
