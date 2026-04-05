import { describe, expect, it } from 'vitest'
import { parseBalancesReportPayload } from './balancesReport'

describe('parseBalancesReportPayload', () => {
  it('parseia camelCase e mantém ordem do array (API já ordena)', () => {
    const p = parseBalancesReportPayload({
      lojistas: [
        { lojistaId: 'a', lojistaName: 'L1', balanceHours: 20 },
        { lojistaId: 'b', lojistaName: 'L2', balanceHours: 5 },
      ],
      lojistaBonificadoPlates: [{ plate: 'ZZZ9999', balanceHours: 3 }],
      clientPlates: [
        { plate: 'BBB2222', balanceHours: 10, expirationDate: null },
        { plate: 'AAA1111', balanceHours: 1, expirationDate: null },
      ],
    })
    expect(p.lojistas).toHaveLength(2)
    expect(p.lojistas[0].balanceHours).toBe(20)
    expect(p.lojistaBonificadoPlates).toHaveLength(1)
    expect(p.lojistaBonificadoPlates[0].plate).toBe('ZZZ9999')
    expect(p.clientPlates[0].plate).toBe('BBB2222')
  })

  it('aceita snake_case nos itens', () => {
    const p = parseBalancesReportPayload({
      lojistas: [{ lojista_id: 'x', lojista_name: 'Z', balance_hours: 3 }],
      lojista_bonificado_plates: [{ plate: 'ABC1D23', balance_hours: 2 }],
      client_plates: [{ plate: 'ZZZ9K99', balance_hours: 7, expiration_date: '2027-01-01T00:00:00Z' }],
    })
    expect(p.lojistas[0].lojistaName).toBe('Z')
    expect(p.lojistaBonificadoPlates[0].balanceHours).toBe(2)
    expect(p.clientPlates[0].expirationDate).toContain('2027')
  })
})
