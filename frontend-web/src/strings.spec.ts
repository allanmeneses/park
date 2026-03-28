import { describe, expect, it } from 'vitest'
import { STRINGS } from './strings'

describe('STRINGS', () => {
  it('has login button', () => {
    expect(STRINGS.B1).toBe('Entrar')
  })
})
