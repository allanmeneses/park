import axios, { type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { getJwtRole, parseJwtPayload } from '@/lib/jwt'
import { getActiveParkingId } from '@/session/activeParking'

export const STORAGE_ACCESS = 'parking.v1.access'
export const STORAGE_REFRESH = 'parking.v1.refresh'

declare module 'axios' {
  export interface InternalAxiosRequestConfig {
    _retry?: boolean
  }
}

export function createApi(): AxiosInstance {
  const base =
    import.meta.env.VITE_API_BASE ||
    (import.meta.env.DEV ? 'http://localhost:8080/api/v1' : '')
  if (!base) {
    throw new Error(
      'VITE_API_BASE não definido: crie frontend-web/.env.development com VITE_API_BASE=http://localhost:8080/api/v1',
    )
  }
  if (import.meta.env.DEV && !import.meta.env.VITE_API_BASE) {
    console.warn(
      '[parking] VITE_API_BASE ausente; a usar fallback http://localhost:8080/api/v1 (defina em .env.development).',
    )
  }
  const api = axios.create({
    baseURL: base,
    headers: { 'Content-Type': 'application/json' },
  })

  api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
    const token = sessionStorage.getItem(STORAGE_ACCESS)
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
      try {
        const role = getJwtRole(parseJwtPayload(token))
        if (role === 'SUPER_ADMIN') {
          const pid = getActiveParkingId()
          if (pid) config.headers['X-Parking-Id'] = pid
        }
      } catch {
        /* ignore */
      }
    }
    return config
  })

  let refreshing = false

  api.interceptors.response.use(
    (r) => r,
    async (err: unknown) => {
      const ax = axios.isAxiosError(err) ? err : null
      const original = ax?.config as InternalAxiosRequestConfig | undefined
      if (!ax || !original || original._retry) throw err
      if (original.url?.includes('/auth/login')) throw err
      if (ax.response?.status !== 401) throw err

      const refresh = localStorage.getItem(STORAGE_REFRESH)
      if (!refresh || refreshing) throw err

      refreshing = true
      original._retry = true
      try {
        const { data } = await axios.post<{
          access_token?: string
          refresh_token?: string
          accessToken?: string
          refreshToken?: string
          expires_in?: number
          expiresIn?: number
        }>(`${base}/auth/refresh`, { refreshToken: refresh })
        const at = data.access_token ?? data.accessToken
        const rtok = data.refresh_token ?? data.refreshToken
        if (!at || !rtok) throw new Error('refresh payload')
        sessionStorage.setItem(STORAGE_ACCESS, at)
        localStorage.setItem(STORAGE_REFRESH, rtok)
        original.headers.Authorization = `Bearer ${at}`
        return api.request(original)
      } finally {
        refreshing = false
      }
    },
  )

  return api
}
