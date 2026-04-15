const ROLE_CLAIM =
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' as const

export function parseJwtPayload(token: string): Record<string, unknown> {
  const parts = token.split('.')
  if (parts.length < 2) throw new Error('JWT inválido.')
  const body = parts[1]!.replace(/-/g, '+').replace(/_/g, '/')
  const pad = body.length % 4 === 0 ? '' : '='.repeat(4 - (body.length % 4))
  const json = atob(body + pad)
  return JSON.parse(json) as Record<string, unknown>
}

export function getJwtRole(payload: Record<string, unknown>): string | undefined {
  const r = payload[ROLE_CLAIM] ?? payload.role
  return typeof r === 'string' ? r : undefined
}

export function getJwtExpEpoch(payload: Record<string, unknown>): number | undefined {
  const exp = payload.exp
  if (typeof exp === 'number') return exp
  if (typeof exp === 'string') {
    const n = Number(exp)
    return Number.isFinite(n) ? n : undefined
  }
  return undefined
}

/** Claim `parking_id` no access token (ADMIN/MANAGER/OPERATOR do tenant). */
export function getJwtParkingId(payload: Record<string, unknown>): string | undefined {
  const pid = payload.parking_id
  return typeof pid === 'string' && pid.length > 0 ? pid : undefined
}
