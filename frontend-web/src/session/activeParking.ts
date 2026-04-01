const KEY = 'parking.v1.activeParkingId'
let activeParkingId: string | null = null

export function setActiveParkingId(id: string | null): void {
  activeParkingId = id
  if (id) sessionStorage.setItem(KEY, id)
  else sessionStorage.removeItem(KEY)
}

export function getActiveParkingId(): string | null {
  if (!activeParkingId) activeParkingId = sessionStorage.getItem(KEY)
  return activeParkingId
}
