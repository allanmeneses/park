import { describe, expect, it } from 'vitest'
import { isValidParkingUuid } from './uuidParking'

describe('isValidParkingUuid', () => {
  it('accepts v4', () => {
    expect(isValidParkingUuid('550e8400-e29b-41d4-a716-446655440000')).toBe(true)
  })

  it('rejects invalid', () => {
    expect(isValidParkingUuid('not-a-uuid')).toBe(false)
  })
})
