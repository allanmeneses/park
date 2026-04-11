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

export type RechargePackageDto = {
  id: string
  display_name: string
  scope: string
  hours: number
  price: string
  is_promo: boolean
  sort_order: number
  active: boolean
}

export function rechargePackageFromApi(raw: Record<string, unknown>): RechargePackageDto {
  const promoRaw = raw.is_promo ?? raw.isPromo
  const activeRaw = raw.active ?? raw.Active
  const orderRaw = raw.sort_order ?? raw.sortOrder
  return {
    id: str(raw.id),
    display_name: str(raw.display_name ?? raw.displayName),
    scope: str(raw.scope ?? raw.Scope),
    hours: Number(raw.hours ?? raw.Hours ?? 0),
    price: str(raw.price ?? raw.Price),
    is_promo: promoRaw === true || promoRaw === 'true' || promoRaw === 1,
    sort_order: Number(orderRaw ?? 0),
    active: activeRaw == null ? true : activeRaw === true || activeRaw === 'true' || activeRaw === 1,
  }
}

function priceToNumber(value: string): number {
  const n = Number(value.replace(',', '.'))
  return Number.isFinite(n) ? n : 0
}

export function compareRechargePackages(a: RechargePackageDto, b: RechargePackageDto): number {
  return (
    a.sort_order - b.sort_order ||
    Number(b.is_promo) - Number(a.is_promo) ||
    priceToNumber(a.price) - priceToNumber(b.price) ||
    a.hours - b.hours ||
    a.display_name.localeCompare(b.display_name, 'pt-BR')
  )
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
