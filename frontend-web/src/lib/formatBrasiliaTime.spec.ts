import { describe, expect, it } from 'vitest'
import { formatApiInstantBrasilia } from './formatBrasiliaTime'

describe('formatApiInstantBrasilia', () => {
  it('formata instante UTC em pt-BR no fuso America/Sao_Paulo', () => {
    const s = formatApiInstantBrasilia('2026-04-03T23:59:37.041Z')
    expect(s).toMatch(/03\/04\/2026/)
    expect(s).toMatch(/20:59/)
  })

  it('devolve string original se não parsear', () => {
    expect(formatApiInstantBrasilia('')).toBe('')
    expect(formatApiInstantBrasilia('x')).toBe('x')
  })
})
