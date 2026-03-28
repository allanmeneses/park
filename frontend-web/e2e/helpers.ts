import { createHmac } from 'node:crypto'

export const apiOrigin = process.env.E2E_API_ORIGIN ?? 'http://127.0.0.1:8080'
export const apiV1 = `${apiOrigin}/api/v1`

export function pickToken(body: Record<string, unknown>): string {
  const t = body.access_token ?? body.accessToken
  if (typeof t !== 'string' || !t) throw new Error('login sem access_token')
  return t
}

export function pickRefreshToken(body: Record<string, unknown>): string {
  const t = body.refresh_token ?? body.refreshToken
  if (typeof t !== 'string' || !t) throw new Error('login sem refresh_token')
  return t
}

export function pickParkingId(body: Record<string, unknown>): string {
  const p = body.parking_id ?? body.parkingId
  if (typeof p !== 'string' && typeof p !== 'number') {
    throw new Error('tenant sem parking_id')
  }
  return String(p)
}

export function pickPaymentId(body: Record<string, unknown>): string {
  const p = body.payment_id ?? body.paymentId
  if (typeof p !== 'string') throw new Error('checkout sem payment_id')
  return p
}

export function webhookSignature(rawBody: string, secret: string): string {
  return createHmac('sha256', secret).update(rawBody).digest('hex')
}
