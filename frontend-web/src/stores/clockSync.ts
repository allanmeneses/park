import { defineStore } from 'pinia'
import axios from 'axios'
import { getResolvedApiBase } from '@/api/http'
import {
  healthUrlFromApiBase,
  isDeviceClockAcceptable,
  serverTimeFromHealthPayload,
} from '@/lib/deviceClockValidation'

export const useClockSyncStore = defineStore('clockSync', {
  state: () => ({
    /** Só bloqueia quando há rede; offline o operador usa o relógio local (manual). */
    blockWhenOnline: false,
    checking: false,
  }),
  actions: {
    async runCheckOnline(): Promise<void> {
      if (typeof navigator !== 'undefined' && !navigator.onLine) {
        this.blockWhenOnline = false
        return
      }
      this.checking = true
      try {
        const url = healthUrlFromApiBase(getResolvedApiBase())
        const { data } = await axios.get<unknown>(url, { timeout: 12_000 })
        const st = serverTimeFromHealthPayload(data)
        if (st == null) {
          this.blockWhenOnline = false
          return
        }
        this.blockWhenOnline = !isDeviceClockAcceptable(st, new Date())
      } catch {
        this.blockWhenOnline = false
      } finally {
        this.checking = false
      }
    },

    /** Devolve função de cleanup (interval + listeners). */
    registerListeners(): () => void {
      if (typeof window === 'undefined') {
        return () => {}
      }
      const onOnline = (): void => {
        void this.runCheckOnline()
      }
      const onOffline = (): void => {
        this.blockWhenOnline = false
      }
      const onVis = (): void => {
        if (document.visibilityState === 'visible') void this.runCheckOnline()
      }
      window.addEventListener('online', onOnline)
      window.addEventListener('offline', onOffline)
      document.addEventListener('visibilitychange', onVis)
      void this.runCheckOnline()
      const id = window.setInterval(() => void this.runCheckOnline(), 60_000)
      return () => {
        window.removeEventListener('online', onOnline)
        window.removeEventListener('offline', onOffline)
        document.removeEventListener('visibilitychange', onVis)
        window.clearInterval(id)
      }
    },
  },
})
