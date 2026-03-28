<template>
  <div class="page">
    <h1>Histórico</h1>
    <p v-if="state === 'loading'">Carregando…</p>
    <p v-else-if="state === 'error'" class="err">{{ err }}</p>
    <p v-else-if="!items.length">Sem movimentos.</p>
    <ul v-else style="list-style: none; padding: 0">
      <li
        v-for="it in items"
        :key="it.id"
        style="border: 1px solid #eee; padding: 0.75rem; margin-bottom: 0.5rem"
      >
        <strong>{{ historyKindLabel(it.kind) }}</strong>
        — {{ formatHistoryDeltaHours(it.kind, it.deltaHours) }}
        <template v-if="it.kind === 'PURCHASE'">
          — {{ formatHistoryAmountBrl(it.amount) }}
        </template>
        — {{ formatHistoryWhen(it.createdAt) }}
      </li>
    </ul>
    <button
      v-if="nextCursor && state === 'ready'"
      type="button"
      class="btn-primary"
      style="margin-top: 1rem"
      aria-label="Carregar mais"
      @click="loadMore"
    >
      Carregar mais
    </button>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/cliente')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import {
  formatHistoryAmountBrl,
  formatHistoryDeltaHours,
  formatHistoryWhen,
  historyKindLabel,
  walletHistoryItemFromApi,
  type WalletHistoryItem,
} from '@/lib/walletHistory'

const api = inject<AxiosInstance>('api')!
const items = ref<WalletHistoryItem[]>([])
const nextCursor = ref<string | null>(null)
const state = ref<'loading' | 'ready' | 'error'>('loading')
const err = ref('')

async function fetchPage(cursor?: string | null): Promise<void> {
  const params: Record<string, string> = { limit: '50' }
  if (cursor) params.cursor = cursor
  const { data } = await api.get<{
    items?: Record<string, unknown>[]
    next_cursor?: string | null
    nextCursor?: string | null
  }>('/client/history', { params })
  const raw = data.items ?? []
  const mapped = raw.map((x) => walletHistoryItemFromApi(x as Record<string, unknown>))
  if (cursor) items.value = items.value.concat(mapped)
  else items.value = mapped
  nextCursor.value = data.next_cursor ?? data.nextCursor ?? null
}

async function loadMore(): Promise<void> {
  if (!nextCursor.value) return
  state.value = 'loading'
  err.value = ''
  try {
    await fetchPage(nextCursor.value)
    state.value = 'ready'
  } catch (e: unknown) {
    state.value = 'error'
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro ao carregar.'
  }
}

onMounted(() => {
  void (async () => {
    state.value = 'loading'
    err.value = ''
    try {
      await fetchPage(null)
      state.value = 'ready'
    } catch (e: unknown) {
      state.value = 'error'
      if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
      else err.value = 'Erro ao carregar.'
    }
  })()
})
</script>
