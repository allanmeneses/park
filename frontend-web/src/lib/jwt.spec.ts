import { describe, expect, it } from 'vitest'
import { getJwtExpEpoch, getJwtRole, parseJwtPayload } from './jwt'

function b64url(obj: object): string {
  const s = JSON.stringify(obj)
  return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

describe('jwt', () => {
  it('parses payload', () => {
    const p = { role: 'OPERATOR', exp: 9999999999 }
    const t = `x.${b64url(p)}.y`
    const out = parseJwtPayload(t)
    expect(getJwtRole(out)).toBe('OPERATOR')
    expect(getJwtExpEpoch(out)).toBe(9999999999)
  })

  it('reads long role claim', () => {
    const roleUri = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    const p = { [roleUri]: 'ADMIN' }
    const t = `x.${b64url(p)}.y`
    expect(getJwtRole(parseJwtPayload(t))).toBe('ADMIN')
  })
})
