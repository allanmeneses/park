import { describe, expect, it } from 'vitest'
import { apiErrorMessage } from './errors'

describe('apiErrorMessage', () => {
  it('uses message when present', () => {
    expect(apiErrorMessage({ message: 'X' })).toBe('X')
  })

  it('maps code without message', () => {
    expect(apiErrorMessage({ code: 'NOT_FOUND', message: '' })).toBe(
      'Registro não encontrado.',
    )
  })
})
