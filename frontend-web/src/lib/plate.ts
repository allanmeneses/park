/** SPEC.md §6 — normalizar: maiúsculas, remover espaços e hífens. */

const MERCOSUL = /^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/
const LEGADO = /^[A-Z]{3}[0-9]{4}$/

export function normalizePlate(raw: string): string {
  return raw.replace(/[\s-]/g, '').toUpperCase()
}

export function isValidPlateNormalized(plate: string): boolean {
  return MERCOSUL.test(plate) || LEGADO.test(plate)
}

export function isValidPlate(raw: string): boolean {
  return isValidPlateNormalized(normalizePlate(raw))
}
