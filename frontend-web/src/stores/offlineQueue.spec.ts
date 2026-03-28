import { describe, expect, it, beforeEach, vi } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useOfflineQueueStore } from './offlineQueue'
import type { AxiosInstance } from 'axios'

beforeEach(() => {
  localStorage.clear()
  setActivePinia(createPinia())
})

describe('offlineQueue', () => {
  it('enqueues and drains FIFO', async () => {
    const store = useOfflineQueueStore()
    const client = {
      request: vi
        .fn()
        .mockResolvedValueOnce({})
        .mockResolvedValueOnce({}),
    } as unknown as AxiosInstance

    store.enqueue({
      id_local: '1',
      method: 'POST',
      path: '/a',
      headers: {},
      body: null,
      created_at_epoch: 1,
    })
    store.enqueue({
      id_local: '2',
      method: 'POST',
      path: '/b',
      headers: {},
      body: { x: 1 },
      created_at_epoch: 2,
    })

    await store.drain(client)

    expect(client.request).toHaveBeenCalledTimes(2)
    expect(client.request).toHaveBeenNthCalledWith(1, expect.objectContaining({ url: '/a' }))
    expect(client.request).toHaveBeenNthCalledWith(2, expect.objectContaining({ url: '/b' }))
    expect(store.items.length).toBe(0)
  })
})
