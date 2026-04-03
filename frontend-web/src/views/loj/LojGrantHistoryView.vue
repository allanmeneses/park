<template>
  <div class="page">
    <h1>Extrato de bonificacoes</h1>
    <p class="sub">Creditos que voce concedeu a clientes (UTC).</p>
    <div class="row">
      <div class="field">
        <label for="from">De (opcional)</label>
        <input id="from" v-model="fromDt" type="datetime-local" aria-label="Data inicial" />
      </div>
      <div class="field">
        <label for="to">Ate (opcional)</label>
        <input id="to" v-model="toDt" type="datetime-local" aria-label="Data final" />
      </div>
    </div>
    <div class="field">
      <label for="fplate">Placa (opcional)</label>
      <input id="fplate" v-model="fPlate" type="text" maxlength="10" aria-label="Filtrar por placa" />
    </div>
    <button type="button" class="btn-primary" aria-label="Aplicar filtros" @click="load">Aplicar filtros</button>
    <p v-if="err" class="err">{{ err }}</p>
    <p v-if="!items.length && !err" class="sub">Nenhum registo.</p>
    <ul v-else class="list">
      <li v-for="it in items" :key="String(it.id)">
        <strong>{{ formatDt(it.created_at) }}</strong>
        - placa <span class="mono">{{ it.plate }}</span>
        - <strong>{{ it.hours }}</strong> h
        - modo: <strong>{{ grantModeText(it.grant_mode) }}</strong>
      </li>
    </ul>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/lojista')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'

type Item = { id: string; created_at: string; plate: string; hours: number; grant_mode: string }

const api = inject<AxiosInstance>('api')!

const fromDt = ref('')
const toDt = ref('')
const fPlate = ref('')
const items = ref<Item[]>([])
const err = ref('')

function toIsoParam(local: string): string | null {
  if (!local.trim()) return null
  const d = new Date(local)
  if (Number.isNaN(d.getTime())) return null
  return d.toISOString()
}

function formatDt(iso: string): string {
  try {
    const d = new Date(iso)
    return d.toLocaleString('pt-BR', { timeZone: 'UTC' }) + ' UTC'
  } catch {
    return iso
  }
}

function grantModeText(mode: string): string {
  return mode === 'ON_SITE' ? 'com veiculo no patio' : 'antecipado'
}

async function load(): Promise<void> {
  err.value = ''
  const q = new URLSearchParams()
  const f = toIsoParam(fromDt.value)
  const t = toIsoParam(toDt.value)
  if (f) q.set('from', f)
  if (t) q.set('to', t)
  if (fPlate.value.trim()) q.set('plate', fPlate.value.trim().toUpperCase())
  const path = `/lojista/grant-client/history${q.toString() ? `?${q}` : ''}`
  try {
    const { data } = await api.get<{ items?: Record<string, unknown>[] }>(path)
    items.value = (data.items ?? []).map((x) => ({
      id: String(x.id ?? ''),
      created_at: String(x.created_at ?? x.createdAt ?? ''),
      plate: String(x.plate ?? ''),
      hours: Number(x.hours ?? 0),
      grant_mode: String(x.grant_mode ?? 'ADVANCE'),
    }))
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro ao carregar.'
  }
}

onMounted(() => {
  void load()
})
</script>

<style scoped>
.sub {
  color: #616161;
}
.row {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
}
.field {
  margin: 0.5rem 0;
}
.err {
  color: #c62828;
}
.list {
  margin-top: 1rem;
  padding-left: 1.2rem;
}
.mono {
  font-family: ui-monospace, monospace;
}
</style>
