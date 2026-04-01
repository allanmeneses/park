/**
 * Espelha Parking.Application.Checkout.CheckoutMath (horas faturáveis = término do período).
 * Usa Date em UTC para comparar com ISO vindo da API.
 */
export function computeBillableHours(entry: Date, exit: Date): number {
  const sec = (exit.getTime() - entry.getTime()) / 1000
  if (sec <= 0) return 0
  return Math.ceil(sec / 3600)
}
