import { describe, expect, it } from 'vitest'
import { ticketLojistaBenefitsFromPayload } from './ticketLojistaBenefit'

describe('ticketLojistaBenefitsFromPayload', () => {
  it('null ou não-array → []', () => {
    expect(ticketLojistaBenefitsFromPayload(null)).toEqual([])
    expect(ticketLojistaBenefitsFromPayload({})).toEqual([])
  })

  it('lê array camelCase', () => {
    const list = ticketLojistaBenefitsFromPayload([
      {
        lojistaId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
        lojistaName: 'Loja A',
        hoursAvailable: 2,
        hoursGrantedTotal: 3,
      },
    ])
    expect(list).toEqual([
      {
        lojistaId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
        lojistaName: 'Loja A',
        hoursAvailable: 2,
        hoursGrantedTotal: 3,
      },
    ])
  })

  it('omite entradas com hoursAvailable 0', () => {
    const list = ticketLojistaBenefitsFromPayload([
      { lojistaName: 'X', hoursAvailable: 0, hoursGrantedTotal: 5 },
      { lojistaName: 'Y', hoursAvailable: 1, hoursGrantedTotal: 1 },
    ])
    expect(list).toEqual([{ lojistaId: '', lojistaName: 'Y', hoursAvailable: 1, hoursGrantedTotal: 1 }])
  })

  it('lê snake_case nos itens', () => {
    const list = ticketLojistaBenefitsFromPayload([
      {
        lojista_id: 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff',
        lojista_name: 'Z',
        hours_available: 4,
        hours_granted_total: 4,
      },
    ])
    expect(list).toEqual([
      { lojistaId: 'bbbbbbbb-cccc-dddd-eeee-ffffffffffff', lojistaName: 'Z', hoursAvailable: 4, hoursGrantedTotal: 4 },
    ])
  })
})
