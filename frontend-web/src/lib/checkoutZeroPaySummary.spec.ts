import { describe, expect, it } from 'vitest'
import { checkoutZeroPaySummaryLines } from './checkoutZeroPaySummary'

describe('checkoutZeroPaySummaryLines', () => {
  it('lista convênio antes da carteira comprada quando ambos consumiram horas', () => {
    const lines = checkoutZeroPaySummaryLines(3, 2, 1)
    expect(lines[0]).toBe('Saída registrada. Nada a pagar.')
    expect(lines[1]).toBe('Total faturável: 3 h.')
    expect(lines[2]).toContain('Convênio')
    expect(lines[3]).toContain('Carteira comprada')
  })

  it('omite linhas de consumo quando zero', () => {
    expect(checkoutZeroPaySummaryLines(1, 1, 0)).toEqual([
      'Saída registrada. Nada a pagar.',
      'Total faturável: 1 h.',
      'Convênio (bonificado): −1 h.',
    ])
  })
})
