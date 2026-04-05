import { str } from './apiDto'

export type TicketLojistaBenefit = {
  lojistaId: string
  lojistaName: string
  hoursAvailable: number
  hoursGrantedTotal: number
}

function intFromUnknown(v: unknown): number {
  if (v == null) return 0
  const n = Number(v)
  return Number.isFinite(n) ? Math.trunc(n) : 0
}

function itemFromUnknown(raw: unknown): TicketLojistaBenefit | null {
  if (typeof raw !== 'object' || raw === null) return null
  const o = raw as Record<string, unknown>
  const hoursAvailable = intFromUnknown(o.hoursAvailable ?? o.hours_available)
  if (hoursAvailable <= 0) return null
  return {
    lojistaId: str(o.lojistaId ?? o.lojista_id),
    lojistaName: str(o.lojistaName ?? o.lojista_name),
    hoursAvailable,
    hoursGrantedTotal: intFromUnknown(o.hoursGrantedTotal ?? o.hours_granted_total),
  }
}

/** GET /tickets/:id — `lojistaBenefits` (array); só itens com saldo bonificado disponível (`hoursAvailable` &gt; 0). */
export function ticketLojistaBenefitsFromPayload(raw: unknown): TicketLojistaBenefit[] {
  if (raw === null || raw === undefined) return []
  if (!Array.isArray(raw)) return []
  const out: TicketLojistaBenefit[] = []
  for (const el of raw) {
    const it = itemFromUnknown(el)
    if (it) out.push(it)
  }
  return out
}
