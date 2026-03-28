import { defineStore } from 'pinia'
import { ref, shallowRef } from 'vue'
import type { AxiosInstance } from 'axios'
import { getJwtExpEpoch, getJwtRole, parseJwtPayload } from '@/lib/jwt'
import { setActiveParkingId } from '@/session/activeParking'
import { STORAGE_ACCESS, STORAGE_REFRESH } from '@/api/http'

let refreshTimer: ReturnType<typeof setTimeout> | null = null

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref<string | null>(null)
  const refreshToken = ref<string | null>(null)
  const expiresAtEpoch = ref<number | null>(null)
  const role = ref<string | null>(null)
  const api = shallowRef<AxiosInstance | null>(null)

  function clearTimer(): void {
    if (refreshTimer != null) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }
  }

  function loadFromStorage(): void {
    accessToken.value = sessionStorage.getItem(STORAGE_ACCESS)
    refreshToken.value = localStorage.getItem(STORAGE_REFRESH)
    if (accessToken.value) {
      try {
        const p = parseJwtPayload(accessToken.value)
        role.value = getJwtRole(p) ?? null
        expiresAtEpoch.value = getJwtExpEpoch(p) ?? null
      } catch {
        role.value = null
        expiresAtEpoch.value = null
      }
    } else {
      role.value = null
      expiresAtEpoch.value = null
    }
  }

  function scheduleRefresh(client: AxiosInstance): void {
    clearTimer()
    const rt = refreshToken.value
    if (!rt) return

    let delaySec: number
    if (expiresAtEpoch.value != null) {
      delaySec = Math.max(1, expiresAtEpoch.value - Math.floor(Date.now() / 1000) - 120)
    } else {
      delaySec = 28080
    }

    refreshTimer = setTimeout(async () => {
      try {
        const { data } = await client.post<{
          access_token: string
          refresh_token: string
          expires_in: number
        }>('/auth/refresh', { refreshToken: rt })
        setTokens(client, data.access_token, data.refresh_token, data.expires_in)
      } catch {
        clear()
      }
    }, delaySec * 1000)
  }

  function setTokens(
    client: AxiosInstance,
    access: string,
    refresh: string,
    expiresIn: number,
  ): void {
    sessionStorage.setItem(STORAGE_ACCESS, access)
    localStorage.setItem(STORAGE_REFRESH, refresh)
    accessToken.value = access
    refreshToken.value = refresh
    try {
      const p = parseJwtPayload(access)
      role.value = getJwtRole(p) ?? null
      const exp = getJwtExpEpoch(p)
      expiresAtEpoch.value =
        exp ?? Math.floor(Date.now() / 1000) + expiresIn
    } catch {
      expiresAtEpoch.value = Math.floor(Date.now() / 1000) + expiresIn
    }
    scheduleRefresh(client)
  }

  function clear(): void {
    clearTimer()
    sessionStorage.removeItem(STORAGE_ACCESS)
    localStorage.removeItem(STORAGE_REFRESH)
    accessToken.value = null
    refreshToken.value = null
    expiresAtEpoch.value = null
    role.value = null
    setActiveParkingId(null)
  }

  function bindApi(client: AxiosInstance): void {
    api.value = client
  }

  return {
    accessToken,
    refreshToken,
    expiresAtEpoch,
    role,
    api,
    loadFromStorage,
    scheduleRefresh,
    setTokens,
    clear,
    bindApi,
  }
})
