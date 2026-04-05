/** Valores monetários da API como string "0.00" (InvariantCulture) ou número. */

export function isZeroMoneyAmount(amountRaw: unknown): boolean {
  if (amountRaw == null) return true
  if (typeof amountRaw === 'number') return Number.isFinite(amountRaw) && Math.abs(amountRaw) < 1e-9
  const s = String(amountRaw).replace(',', '.').trim()
  if (s === '') return true
  const n = Number(s)
  return Number.isFinite(n) && Math.abs(n) < 1e-9
}
