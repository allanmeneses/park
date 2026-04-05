import { describe, expect, it } from 'vitest'
import { isRouteAllowedForRole } from './roleAccess'

describe('isRouteAllowedForRole', () => {
  it('allows login for any role', () => {
    expect(isRouteAllowedForRole(null, 'login')).toBe(true)
    expect(isRouteAllowedForRole('CLIENT', 'login')).toBe(true)
  })

  it('allows loj_register for any role (público no guard)', () => {
    expect(isRouteAllowedForRole(null, 'loj_register')).toBe(true)
    expect(isRouteAllowedForRole('ADMIN', 'loj_register')).toBe(true)
  })

  it('OPERATOR cannot open gestor', () => {
    expect(isRouteAllowedForRole('OPERATOR', 'mgr_dashboard')).toBe(false)
  })

  it('MANAGER can open gestor and operador', () => {
    expect(isRouteAllowedForRole('MANAGER', 'mgr_dashboard')).toBe(true)
    expect(isRouteAllowedForRole('MANAGER', 'mgr_movements')).toBe(true)
    expect(isRouteAllowedForRole('MANAGER', 'mgr_analytics')).toBe(true)
    expect(isRouteAllowedForRole('MANAGER', 'mgr_balances_report')).toBe(true)
    expect(isRouteAllowedForRole('MANAGER', 'op_home')).toBe(true)
  })

  it('MANAGER cannot open lojista invites (tenant admin only)', () => {
    expect(isRouteAllowedForRole('MANAGER', 'mgr_lojista_invites')).toBe(false)
  })

  it('ADMIN and SUPER_ADMIN can open lojista invites', () => {
    expect(isRouteAllowedForRole('ADMIN', 'mgr_lojista_invites')).toBe(true)
    expect(isRouteAllowedForRole('SUPER_ADMIN', 'mgr_lojista_invites')).toBe(true)
  })

  it('CLIENT only client routes', () => {
    expect(isRouteAllowedForRole('CLIENT', 'cli_wallet')).toBe(true)
    expect(isRouteAllowedForRole('CLIENT', 'op_home')).toBe(false)
  })

  it('SUPER_ADMIN includes adm_tenant', () => {
    expect(isRouteAllowedForRole('SUPER_ADMIN', 'adm_tenant')).toBe(true)
  })

  it('ADMIN cannot open adm_tenant (only super creates tenants)', () => {
    expect(isRouteAllowedForRole('ADMIN', 'adm_tenant')).toBe(false)
    expect(isRouteAllowedForRole('ADMIN', 'mgr_dashboard')).toBe(true)
  })

  it('ADMIN has same gestor insights/analytics routes as MANAGER', () => {
    expect(isRouteAllowedForRole('ADMIN', 'mgr_movements')).toBe(true)
    expect(isRouteAllowedForRole('ADMIN', 'mgr_analytics')).toBe(true)
    expect(isRouteAllowedForRole('ADMIN', 'mgr_balances_report')).toBe(true)
  })

  it('SUPER_ADMIN can open balances report with tenant', () => {
    expect(isRouteAllowedForRole('SUPER_ADMIN', 'mgr_balances_report')).toBe(true)
  })

  it('unknown role denies', () => {
    expect(isRouteAllowedForRole('GUEST', 'op_home')).toBe(false)
  })

  it('LOJISTA can open bonificação e extrato de bonificações', () => {
    expect(isRouteAllowedForRole('LOJISTA', 'loj_grant')).toBe(true)
    expect(isRouteAllowedForRole('LOJISTA', 'loj_grant_history')).toBe(true)
    expect(isRouteAllowedForRole('CLIENT', 'loj_grant')).toBe(false)
  })
})
