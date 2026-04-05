/** Linhas do alerta quando checkout encerra com amount zero (SPEC: convênio antes da carteira). */
export function checkoutZeroPaySummaryLines(
  hoursTotal: number,
  hoursLojista: number,
  hoursCliente: number,
): string[] {
  const parts: string[] = ['Saída registrada. Nada a pagar.']
  if (hoursTotal > 0) parts.push(`Total faturável: ${hoursTotal} h.`)
  if (hoursLojista > 0) parts.push(`Convênio (bonificado): −${hoursLojista} h.`)
  if (hoursCliente > 0) parts.push(`Carteira comprada: −${hoursCliente} h.`)
  return parts
}
