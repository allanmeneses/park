/** Normaliza chaves camelCase (.NET) e snake_case legado. */

/** Resposta de POST /lojista/grant-client (serialização pode ser snake ou camel). */
export function grantClientBalanceHours(raw: Record<string, unknown>): number | null {
  const v = raw.client_balance_hours ?? raw.clientBalanceHours
  if (v == null) return null
  const n = Number(v)
  return Number.isFinite(n) ? n : null
}

export function str(v: unknown): string {
  if (v == null) return ''
  return String(v)
}

export function ticketRowFromApi(raw: Record<string, unknown>): {
  id: string
  plate: string
  entry_time: string
  status: string
} {
  return {
    id: str(raw.id),
    plate: str(raw.plate ?? raw.Plate),
    entry_time: str(raw.entryTime ?? raw.entry_time),
    status: str(raw.status ?? raw.Status),
  }
}
