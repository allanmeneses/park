/** Segundos inteiros decorridos entre dois instantes (mínimo 0). */
export function elapsedWholeSeconds(entry: Date, end: Date): number {
  const sec = Math.floor((end.getTime() - entry.getTime()) / 1000)
  return Math.max(0, sec)
}

/** Texto legível pt-BR — sempre horas, minutos e segundos (contador ao vivo). */
export function formatElapsedPtBr(totalSeconds: number): string {
  const s = Math.max(0, Math.floor(totalSeconds))
  const h = Math.floor(s / 3600)
  const m = Math.floor((s % 3600) / 60)
  const sec = s % 60
  return `${h} h ${m} min ${sec} s`
}
