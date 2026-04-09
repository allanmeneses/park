import { describe, expect, it } from 'vitest'
import { STRINGS } from './strings'

describe('STRINGS', () => {
  it('has login button', () => {
    expect(STRINGS.B1).toBe('Entrar')
  })
  it('has card PSP checkout copy', () => {
    expect(STRINGS.S27.length).toBeGreaterThan(20)
    expect(STRINGS.B33).toContain('pagamento')
  })
})
