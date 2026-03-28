import { describe, expect, it, beforeEach } from 'vitest'
import { getActiveParkingId, setActiveParkingId } from './activeParking'

beforeEach(() => {
  setActiveParkingId(null)
})

describe('activeParking', () => {
  it('roundtrips', () => {
    setActiveParkingId('550e8400-e29b-41d4-a716-446655440000')
    expect(getActiveParkingId()).toBe('550e8400-e29b-41d4-a716-446655440000')
    setActiveParkingId(null)
    expect(getActiveParkingId()).toBeNull()
  })
})
