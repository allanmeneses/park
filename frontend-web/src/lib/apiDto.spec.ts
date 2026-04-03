import { describe, expect, it } from 'vitest'
import { grantClientBalanceHours, ticketRowFromApi } from './apiDto'

describe('ticketRowFromApi', () => {
  it('maps camelCase', () => {
    const r = ticketRowFromApi({
      id: 'a',
      plate: 'ABC1D23',
      entryTime: '2020-01-01',
      status: 'OPEN',
    })
    expect(r.id).toBe('a')
    expect(r.plate).toBe('ABC1D23')
    expect(r.entry_time).toBe('2020-01-01')
  })
})

describe('grantClientBalanceHours', () => {
  it('reads snake_case', () => {
    expect(grantClientBalanceHours({ client_balance_hours: 3 })).toBe(3)
  })
  it('reads camelCase', () => {
    expect(grantClientBalanceHours({ clientBalanceHours: 2 })).toBe(2)
  })
  it('prefers snake when both present', () => {
    expect(grantClientBalanceHours({ client_balance_hours: 1, clientBalanceHours: 99 })).toBe(1)
  })
})
