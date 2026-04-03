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

  it('maps LOJISTA_INVITE_INVALID', () => {
    expect(apiErrorMessage({ code: 'LOJISTA_INVITE_INVALID', message: '' })).toBe(
      'Código do lojista ou ativação inválidos.',
    )
  })

  it('maps LOJISTA_CREDIT_INSUFFICIENT', () => {
    expect(apiErrorMessage({ code: 'LOJISTA_CREDIT_INSUFFICIENT', message: '' })).toBe(
      'Créditos insuficientes na sua carteira de convênio.',
    )
  })

  it('maps CLIENT_FOR_OTHER_LOJISTA', () => {
    expect(apiErrorMessage({ code: 'CLIENT_FOR_OTHER_LOJISTA', message: '' })).toBe(
      'Esta placa está vinculada a outro convênio.',
    )
  })

  it('maps GRANT_REQUIRES_ACTIVE_TICKET', () => {
    expect(apiErrorMessage({ code: 'GRANT_REQUIRES_ACTIVE_TICKET', message: '' })).toBe(
      'É necessário ticket em aberto para esta placa, ou permita crédito antecipado na carteira.',
    )
  })
})
