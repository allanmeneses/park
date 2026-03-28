/** SPEC_FRONTEND §5.14 / §5.17 — rótulos e formatação do histórico da carteira. */

export type WalletHistoryItem = {
  id: string
  kind: string
  deltaHours: number
  amount: string
  createdAt: string
}

function num(v: unknown): number {
  if (typeof v === 'number' && !Number.isNaN(v)) return v
  if (typeof v === 'string') {
    const n = Number(v.replace(',', '.'))
    return Number.isNaN(n) ? 0 : n
  }
  return 0
}

/** Normaliza item da API (camelCase ou snake_case). */
export function walletHistoryItemFromApi(raw: Record<string, unknown>): WalletHistoryItem {
  const kind = String(raw.kind ?? '')
  const delta =
    num(raw.deltaHours ?? raw.delta_hours)
  const amount = String(raw.amount ?? raw.Amount ?? '0')
  const created = String(raw.createdAt ?? raw.created_at ?? '')
  const id = String(raw.id ?? '')
  return { id, kind, deltaHours: delta, amount, createdAt: created }
}

export function historyKindLabel(kind: string): string {
  if (kind === 'PURCHASE') return 'Compra'
  if (kind === 'USAGE') return 'Uso'
  return kind
}

/** SPEC: sinal + para compra; uso mantém valor (tipicamente negativo) com sinal. */
export function formatHistoryDeltaHours(kind: string, deltaHours: number): string {
  if (kind === 'PURCHASE') return `+${deltaHours} h`
  const n = deltaHours
  if (n < 0) return `${n} h`
  if (n > 0) return `-${n} h`
  return '0 h'
}

export function formatHistoryWhen(iso: string): string {
  try {
    const d = new Date(iso)
    return d.toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return iso
  }
}

export function formatHistoryAmountBrl(amountStr: string): string {
  const n = Number(amountStr.replace(',', '.'))
  if (Number.isNaN(n)) return amountStr
  return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}
