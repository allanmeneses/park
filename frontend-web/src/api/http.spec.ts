import { describe, expect, it } from 'vitest'
import { createApi } from './http'

describe('createApi', () => {
  it('uses VITE_API_BASE', () => {
    const api = createApi()
    expect(api.defaults.baseURL).toBeTruthy()
  })
})
