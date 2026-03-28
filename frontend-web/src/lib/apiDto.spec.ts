import { describe, expect, it } from 'vitest'
import { ticketRowFromApi } from './apiDto'

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
