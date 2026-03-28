/** SUPER_ADMIN: UUID ativo só em memória (SPEC §4.3). */

let activeParkingId: string | null = null

export function setActiveParkingId(id: string | null): void {
  activeParkingId = id
}

export function getActiveParkingId(): string | null {
  return activeParkingId
}
