import { describe, expect, it, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import type { AxiosInstance } from 'axios'
import { useAuthStore } from './auth'
import { STORAGE_ACCESS, STORAGE_REFRESH } from '@/api/http'

beforeEach(() => {
  localStorage.clear()
  sessionStorage.clear()
  setActivePinia(createPinia())
})

function b64url(obj: object): string {
  return btoa(JSON.stringify(obj))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

function fakeAccessToken(role: string, exp: number): string {
  const p = { role, exp }
  return `h.${b64url(p)}.s`
}

describe('auth store', () => {
  it('setTokens persists', () => {
    const client = { post: vi.fn() } as unknown as AxiosInstance
    const store = useAuthStore()
    store.bindApi(client)
    const at = fakeAccessToken('OPERATOR', Math.floor(Date.now() / 1000) + 4000)
    store.setTokens(client, at, 'r1', 3600)
    expect(sessionStorage.getItem(STORAGE_ACCESS)).toBe(at)
    expect(localStorage.getItem(STORAGE_REFRESH)).toBe('r1')
  })

  it('clear removes storage', () => {
    const client = {} as AxiosInstance
    const store = useAuthStore()
    store.bindApi(client)
    store.setTokens(
      client,
      fakeAccessToken('ADMIN', Math.floor(Date.now() / 1000) + 7200),
      'r',
      100,
    )
    store.clear()
    expect(sessionStorage.getItem(STORAGE_ACCESS)).toBeNull()
    expect(localStorage.getItem(STORAGE_REFRESH)).toBeNull()
  })

  it('loadFromStorage reads tokens', () => {
    const at = fakeAccessToken('CLIENT', Math.floor(Date.now() / 1000) + 7200)
    sessionStorage.setItem(STORAGE_ACCESS, at)
    localStorage.setItem(STORAGE_REFRESH, 'rr')
    const store = useAuthStore()
    store.bindApi({} as AxiosInstance)
    store.loadFromStorage()
    expect(store.role).toBe('CLIENT')
  })
})
