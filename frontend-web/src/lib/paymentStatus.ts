import { str } from './apiDto'

export function normalizePaymentStatus(v: unknown): string {
  return str(v).trim().toUpperCase()
}

export function isPaidStatus(v: unknown): boolean {
  return normalizePaymentStatus(v) === 'PAID'
}
