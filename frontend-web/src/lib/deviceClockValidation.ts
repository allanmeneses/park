/** Fuso usado para data civil e exibição operacional (SPEC / manual). */
export const BRASILIA_TZ = 'America/Sao_Paulo'

/** Margem máxima entre relógio do dispositivo e `serverTimeUtc` do GET /health (online). */
export const CLOCK_MAX_SKEW_MS = 5 * 60 * 1000

export function healthUrlFromApiBase(apiBase: string): string {
  const trimmed = apiBase.replace(/\/+$/, '')
  const base = trimmed.endsWith('/api/v1') ? trimmed.slice(0, -'/api/v1'.length) : trimmed
  return `${base.replace(/\/+$/, '')}/health`
}

/** Data civil `YYYY-MM-DD` em America/Sao_Paulo. */
export function calendarDateKeyInBrasilia(d: Date): string {
  return d.toLocaleDateString('en-CA', { timeZone: BRASILIA_TZ })
}

/**
 * Compara instante do servidor (ISO UTC do health) com o relógio local.
 * Se `serverUtcIso` for inválido, devolve true (não bloquear por falta de referência).
 */
export function isDeviceClockAcceptable(serverUtcIso: string, clientNow: Date): boolean {
  const serverMs = Date.parse(serverUtcIso)
  if (Number.isNaN(serverMs)) return true
  const server = new Date(serverMs)
  const skew = Math.abs(server.getTime() - clientNow.getTime())
  if (skew > CLOCK_MAX_SKEW_MS) return false
  return calendarDateKeyInBrasilia(server) === calendarDateKeyInBrasilia(clientNow)
}

export type HealthClockPayload = { ok?: boolean; serverTimeUtc?: string }

export function serverTimeFromHealthPayload(data: unknown): string | null {
  if (data == null || typeof data !== 'object') return null
  const v = (data as Record<string, unknown>).serverTimeUtc
  return typeof v === 'string' && v.length > 0 ? v : null
}
