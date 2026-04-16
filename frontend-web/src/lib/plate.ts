/** SPEC.md §6 — normalizar: maiúsculas, remover espaços e hífens. */

/** Comprimento máximo no `<input>` formatado (AAA-XXXX = 8). */
export const PLATE_DISPLAY_MAX_LENGTH = 8

const MERCOSUL = /^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$/
const LEGADO = /^[A-Z]{3}[0-9]{4}$/

export function normalizePlate(raw: string): string {
  return raw.replace(/[\s-]/g, '').toUpperCase()
}

/** Filtra digitação/colar para até 7 caracteres válidos (Mercosul ou legado). */
export function sanitizePlateInput(raw: string): string {
  const cleaned = normalizePlate(raw).replace(/[^A-Z0-9]/g, '')
  let out = ''
  for (let i = 0; i < cleaned.length && out.length < 7; i++) {
    const c = cleaned[i]
    const pos = out.length
    if (pos < 3) {
      if (/[A-Z]/.test(c)) out += c
    } else if (pos === 3) {
      if (/[0-9]/.test(c)) out += c
    } else if (pos === 4) {
      if (/[A-Z0-9]/.test(c)) out += c
    } else if (/[0-9]/.test(c)) {
      out += c
    }
  }
  return out
}

/** Formato visual AAA-XXXX (hífen só após a3.ª letra; não altera o valor enviado à API). */
export function formatPlateDisplay(normalizedNoHyphen: string): string {
  const s = sanitizePlateInput(normalizedNoHyphen)
  if (s.length <= 3) return s
  return `${s.slice(0, 3)}-${s.slice(3)}`
}

/** Índice no valor sem hífen (0..7) a partir da posição do cursor no campo formatado. */
export function plateDisplayIndexToRawLength(displayIndex: number, displayValue: string): number {
  let raw = 0
  for (let di = 0; di < displayIndex && di < displayValue.length; di++) {
    if (displayValue[di] !== '-') raw++
  }
  return Math.min(raw, 7)
}

/** Posição do cursor no campo formatado a partir do índice no valor sem hífen (0 = antes da 1.ª letra). */
export function plateRawLengthToDisplayIndex(rawLen: number): number {
  if (rawLen <= 0) return 0
  if (rawLen <= 3) return rawLen
  return rawLen + 1
}

export function isValidPlateNormalized(plate: string): boolean {
  return MERCOSUL.test(plate) || LEGADO.test(plate)
}

export function isValidPlate(raw: string): boolean {
  return isValidPlateNormalized(normalizePlate(raw))
}
