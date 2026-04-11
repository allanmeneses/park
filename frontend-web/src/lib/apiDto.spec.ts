import { describe, expect, it } from 'vitest'
import {
  compareRechargePackages,
  grantClientBalanceHours,
  rechargePackageFromApi,
  ticketRowFromApi,
} from './apiDto'

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

describe('rechargePackageFromApi', () => {
  it('maps snake_case package fields', () => {
    const r = rechargePackageFromApi({
      id: 'pkg-1',
      display_name: 'Promo Cliente',
      scope: 'CLIENT',
      hours: 15,
      price: '49,90',
      is_promo: true,
      sort_order: 20,
      active: false,
    })
    expect(r).toEqual({
      id: 'pkg-1',
      display_name: 'Promo Cliente',
      scope: 'CLIENT',
      hours: 15,
      price: '49,90',
      is_promo: true,
      sort_order: 20,
      active: false,
    })
  })

  it('maps camelCase package fields', () => {
    const r = rechargePackageFromApi({
      id: 'pkg-2',
      displayName: 'Convênio 20h',
      scope: 'LOJISTA',
      hours: 20,
      price: '100.00',
      isPromo: false,
      sortOrder: 10,
      active: true,
    })
    expect(r.display_name).toBe('Convênio 20h')
    expect(r.is_promo).toBe(false)
    expect(r.sort_order).toBe(10)
    expect(r.active).toBe(true)
  })
})

describe('compareRechargePackages', () => {
  it('orders by sort_order and then prioritizes promo', () => {
    const items = [
      rechargePackageFromApi({
        id: 'later',
        display_name: 'Depois',
        scope: 'CLIENT',
        hours: 5,
        price: '20.00',
        is_promo: false,
        sort_order: 30,
      }),
      rechargePackageFromApi({
        id: 'promo',
        display_name: 'Promo',
        scope: 'CLIENT',
        hours: 10,
        price: '40.00',
        is_promo: true,
        sort_order: 10,
      }),
      rechargePackageFromApi({
        id: 'base',
        display_name: 'Base',
        scope: 'CLIENT',
        hours: 10,
        price: '35.00',
        is_promo: false,
        sort_order: 10,
      }),
    ]

    items.sort(compareRechargePackages)
    expect(items.map((x) => x.id)).toEqual(['promo', 'base', 'later'])
  })
})
