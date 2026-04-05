import { BRASILIA_TZ } from './deviceClockValidation'

/** Exibe instante API (ISO) como data/hora em Brasília — alinha lista e detalhe do ticket. */
export function formatApiInstantBrasilia(iso: string): string {
  if (!iso) return iso
  const ms = Date.parse(iso)
  if (Number.isNaN(ms)) return iso
  return new Date(ms).toLocaleString('pt-BR', {
    timeZone: BRASILIA_TZ,
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
