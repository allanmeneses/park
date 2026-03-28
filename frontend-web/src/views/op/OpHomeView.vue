<template>
  <div>
    <div v-if="!online" class="banner-offline">{{ STRINGS.S2 }}</div>
    <div class="page">
      <header style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem">
        <h1>Operador</h1>
        <div style="display: flex; gap: 0.5rem; align-items: center">
          <button
            v-if="showGestao"
            type="button"
            class="btn-primary"
            :aria-label="STRINGS.B20"
            @click="$router.push('/gestor')"
          >
            {{ STRINGS.B20 }}
          </button>
          <button type="button" class="btn-primary" aria-label="Registrar problema" @click="problem">
            ⋮
          </button>
        </div>
      </header>
      <button
        type="button"
        class="btn-primary"
        style="width: 100%; margin-bottom: 1rem"
        :disabled="!online"
        :aria-label="STRINGS.B2"
        @click="$router.push('/operador/entrada')"
      >
        {{ STRINGS.B2 }}
      </button>
      <p v-if="state === 'loading'">Carregando…</p>
      <p v-else-if="state === 'error'" class="err">{{ err }}</p>
      <p v-else-if="items.length === 0">{{ STRINGS.S1 }}</p>
      <ul v-else style="list-style: none; padding: 0" role="list">
        <li
          v-for="t in items"
          :key="t.id"
          role="button"
          tabindex="0"
          :aria-label="`Ticket ${t.plate}, status ${t.status}, entrada ${fmt(t.entry_time)}`"
          style="border: 1px solid #eee; padding: 0.75rem; margin-bottom: 0.5rem; cursor: pointer"
          @click="goTicket(t.id)"
          @keydown.enter.prevent="goTicket(t.id)"
          @keydown.space.prevent="goTicket(t.id)"
        >
          <strong>{{ t.plate }}</strong> — {{ t.status }} — {{ fmt(t.entry_time) }}
        </li>
      </ul>
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { STRINGS } from '@/strings'
import { ticketRowFromApi } from '@/lib/apiDto'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const api = inject<AxiosInstance>('api')!
const auth = useAuthStore()
auth.loadFromStorage()
const showGestao = computed(() =>
  ['MANAGER', 'ADMIN', 'SUPER_ADMIN'].includes(auth.role ?? ''),
)

type Row = { id: string; plate: string; entry_time: string; status: string }

const items = ref<Row[]>([])
const state = ref<'loading' | 'ready' | 'error'>('loading')
const err = ref('')
const online = computed(() => (typeof navigator !== 'undefined' ? navigator.onLine : true))

function goTicket(id: string): void {
  void router.push(`/operador/ticket/${id}`)
}

function fmt(iso: string): string {
  try {
    const d = new Date(iso)
    return d.toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return iso
  }
}

async function load(): Promise<void> {
  state.value = 'loading'
  err.value = ''
  try {
    const { data } = await api.get<{ items: Record<string, unknown>[] }>('/tickets/open')
    items.value = (data.items ?? []).map((x) => ticketRowFromApi(x))
    state.value = 'ready'
  } catch (e: unknown) {
    state.value = 'error'
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro ao carregar.'
  }
}

async function problem(): Promise<void> {
  try {
    await api.post('/operator/problem', {})
    alert('Problema registrado.')
  } catch {
    alert('Falha ao registrar.')
  }
}

onMounted(() => {
  void load()
})
</script>
