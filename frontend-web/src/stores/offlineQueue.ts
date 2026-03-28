import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AxiosInstance } from 'axios'

const QUEUE_KEY = 'parking.v1.offline_queue'

export type OfflineQueueItem = {
  id_local: string
  method: 'POST'
  path: string
  headers: Record<string, string>
  body: object | null
  created_at_epoch: number
  attempts: number
}

const BACKOFF_MS = [1000, 2000, 4000, 8000, 16000]

function readQueue(): OfflineQueueItem[] {
  try {
    const raw = localStorage.getItem(QUEUE_KEY)
    if (!raw) return []
    const p = JSON.parse(raw) as OfflineQueueItem[]
    return Array.isArray(p) ? p : []
  } catch {
    return []
  }
}

function writeQueue(q: OfflineQueueItem[]): void {
  localStorage.setItem(QUEUE_KEY, JSON.stringify(q))
}

export const useOfflineQueueStore = defineStore('offlineQueue', () => {
  const items = ref<OfflineQueueItem[]>(readQueue())
  const draining = ref(false)

  function persist(): void {
    writeQueue(items.value)
  }

  function enqueue(item: Omit<OfflineQueueItem, 'attempts'>): void {
    items.value.push({ ...item, attempts: 0 })
    persist()
  }

  /** SPEC §10: até 5 tentativas com backoff; falha final mantém item na fila. */
  async function drain(client: AxiosInstance): Promise<void> {
    if (draining.value) return
    draining.value = true
    try {
      while (items.value.length > 0) {
        const head = items.value[0]!
        let success = false
        for (let attempt = 0; attempt < 5 && !success; attempt++) {
          if (attempt > 0) {
            await new Promise((r) => setTimeout(r, BACKOFF_MS[attempt - 1] ?? 16000))
          }
          try {
            await client.request({
              method: head.method,
              url: head.path,
              headers: { ...head.headers },
              data: head.body ?? undefined,
            })
            success = true
          } catch {
            head.attempts = attempt + 1
            persist()
          }
        }
        if (!success) {
          persist()
          throw new Error('QUEUE_ITEM_FAILED')
        }
        items.value.shift()
        persist()
      }
    } finally {
      draining.value = false
    }
  }

  return { items, draining, enqueue, drain, persist }
})
