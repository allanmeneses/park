/** Resposta de GET /manager/balances-report (camelCase da API). */
export type LojistaBalanceRow = {
  lojistaId: string
  lojistaName: string
  balanceHours: number
}

export type ClientPlateBalanceRow = {
  plate: string
  balanceHours: number
  expirationDate: string | null
}

/** Horas bonificadas (convênio) ainda disponíveis na placa — mesma regra do checkout. */
export type LojistaBonificadoPlateRow = {
  plate: string
  balanceHours: number
}

export type BalancesReportPayload = {
  lojistas: LojistaBalanceRow[]
  lojistaBonificadoPlates: LojistaBonificadoPlateRow[]
  clientPlates: ClientPlateBalanceRow[]
}

function intFromUnknown(v: unknown): number {
  if (v == null) return 0
  const n = Number(v)
  return Number.isFinite(n) ? Math.trunc(n) : 0
}

function str(v: unknown): string {
  if (v == null) return ''
  if (typeof v === 'string') return v
  return String(v)
}

function lojistaFromUnknown(raw: unknown): LojistaBalanceRow | null {
  if (typeof raw !== 'object' || raw === null) return null
  const o = raw as Record<string, unknown>
  return {
    lojistaId: str(o.lojistaId ?? o.lojista_id),
    lojistaName: str(o.lojistaName ?? o.lojista_name),
    balanceHours: intFromUnknown(o.balanceHours ?? o.balance_hours),
  }
}

function clientRowFromUnknown(raw: unknown): ClientPlateBalanceRow | null {
  if (typeof raw !== 'object' || raw === null) return null
  const o = raw as Record<string, unknown>
  const exp = o.expirationDate ?? o.expiration_date
  return {
    plate: str(o.plate),
    balanceHours: intFromUnknown(o.balanceHours ?? o.balance_hours),
    expirationDate: exp == null || exp === '' ? null : str(exp),
  }
}

function bonificadoPlateFromUnknown(raw: unknown): LojistaBonificadoPlateRow | null {
  if (typeof raw !== 'object' || raw === null) return null
  const o = raw as Record<string, unknown>
  return {
    plate: str(o.plate),
    balanceHours: intFromUnknown(o.balanceHours ?? o.balance_hours),
  }
}

export function parseBalancesReportPayload(raw: unknown): BalancesReportPayload {
  if (typeof raw !== 'object' || raw === null) {
    return { lojistas: [], lojistaBonificadoPlates: [], clientPlates: [] }
  }
  const o = raw as Record<string, unknown>
  const lojRaw = o.lojistas ?? o.Lojistas
  const bonRaw = o.lojistaBonificadoPlates ?? o.lojista_bonificado_plates
  const cliRaw = o.clientPlates ?? o.client_plates
  const lojistas: LojistaBalanceRow[] = []
  const lojistaBonificadoPlates: LojistaBonificadoPlateRow[] = []
  const clientPlates: ClientPlateBalanceRow[] = []
  if (Array.isArray(lojRaw)) {
    for (const el of lojRaw) {
      const row = lojistaFromUnknown(el)
      if (row) lojistas.push(row)
    }
  }
  if (Array.isArray(bonRaw)) {
    for (const el of bonRaw) {
      const row = bonificadoPlateFromUnknown(el)
      if (row) lojistaBonificadoPlates.push(row)
    }
  }
  if (Array.isArray(cliRaw)) {
    for (const el of cliRaw) {
      const row = clientRowFromUnknown(el)
      if (row) clientPlates.push(row)
    }
  }
  return { lojistas, lojistaBonificadoPlates, clientPlates }
}
