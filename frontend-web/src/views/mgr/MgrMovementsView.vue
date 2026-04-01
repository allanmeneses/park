<template>
  <div class="page">
    <h1>Insights de Movimentações</h1>
    <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1rem">
      <button type="button" class="btn-secondary" :disabled="loading" @click="quick('24h')">Últimas 24h</button>
      <button type="button" class="btn-secondary" :disabled="loading" @click="quick('7d')">Últimos 7 dias</button>
      <button type="button" class="btn-secondary" :disabled="loading" @click="quick('30d')">Últimos 30 dias</button>
    </div>

    <div style="display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: end">
      <div class="field">
        <label for="from">De (UTC)</label>
        <input id="from" v-model="fromUtc" type="datetime-local" />
      </div>
      <div class="field">
        <label for="to">Até (UTC)</label>
        <input id="to" v-model="toUtc" type="datetime-local" />
      </div>
      <div class="field">
        <label for="kind">Tipo</label>
        <select id="kind" v-model="kind">
          <option value="">Todos</option>
          <option value="TICKET_PAYMENT">Pagamento ticket</option>
          <option value="PACKAGE_PAYMENT">Pagamento pacote</option>
          <option value="LOJISTA_USAGE">Uso lojista</option>
          <option value="CLIENT_USAGE">Uso cliente</option>
        </select>
      </div>
      <button type="button" class="btn-primary" :disabled="loading" @click="load">Aplicar</button>
      <button type="button" class="btn-secondary" @click="$router.push('/gestor')">Voltar</button>
    </div>

    <p v-if="err" class="err">{{ err }}</p>
    <template v-if="data">
      <p>Total ticket: {{ data.insights.total_ticket }}</p>
      <p>Total pacote: {{ data.insights.total_package }}</p>
      <p>Usos convênio lojista: {{ data.insights.usages_lojista }}</p>
      <p>Usos carteira cliente: {{ data.insights.usages_client }}</p>
      <p>Registros: {{ data.count }}</p>

      <table style="width: 100%; margin-top: 0.75rem; border-collapse: collapse">
        <thead>
          <tr>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Quando (UTC)</th>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Tipo</th>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Valor</th>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Método</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="m in data.items" :key="m.ref" style="border-bottom: 1px solid #f0f0f0">
            <td>{{ formatUtc(m.at) }}</td>
            <td>{{ m.kind }}</td>
            <td>{{ money(m.amount) }}</td>
            <td>{{ m.method ?? '—' }}</td>
          </tr>
        </tbody>
      </table>
      <p v-if="!data.items.length">Sem movimentos para os filtros selecionados.</p>
    </template>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'

type Movement = { at: string; kind: string; amount: string; ref: string; method: string | null }
type MovementsResponse = {
  from: string
  to: string
  count: number
  insights: { total_ticket: string; total_package: string; usages_lojista: number; usages_client: number }
  items: Movement[]
}

const api = inject<AxiosInstance>('api')!
const data = ref<MovementsResponse | null>(null)
const err = ref('')
const loading = ref(false)
const fromUtc = ref('')
const toUtc = ref('')
const kind = ref('')

function toInputUtc(date: Date): string {
  const y = date.getUTCFullYear()
  const m = String(date.getUTCMonth() + 1).padStart(2, '0')
  const d = String(date.getUTCDate()).padStart(2, '0')
  const hh = String(date.getUTCHours()).padStart(2, '0')
  const mm = String(date.getUTCMinutes()).padStart(2, '0')
  return `${y}-${m}-${d}T${hh}:${mm}`
}

function parseInputUtc(value: string): string | null {
  if (!value) return null
  const date = new Date(`${value}:00Z`)
  if (Number.isNaN(date.getTime())) return null
  return date.toISOString()
}

function money(value: string): string {
  const n = Number(value)
  if (Number.isNaN(n)) return value
  return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

function formatUtc(value: string): string {
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return d.toISOString().replace('T', ' ').slice(0, 19)
}

function quick(mode: '24h' | '7d' | '30d'): void {
  const now = new Date()
  const from = new Date(now)
  if (mode === '24h') from.setUTCHours(from.getUTCHours() - 24)
  if (mode === '7d') from.setUTCDate(from.getUTCDate() - 7)
  if (mode === '30d') from.setUTCDate(from.getUTCDate() - 30)
  fromUtc.value = toInputUtc(from)
  toUtc.value = toInputUtc(now)
  void load()
}

async function load(): Promise<void> {
  loading.value = true
  err.value = ''
  try {
    const params: Record<string, string> = {}
    const fromIso = parseInputUtc(fromUtc.value)
    const toIso = parseInputUtc(toUtc.value)
    if (fromIso) params.from = fromIso
    if (toIso) params.to = toIso
    if (kind.value.trim()) params.kind = kind.value.trim()
    const { data: body } = await api.get('/manager/movements', { params })
    data.value = body as MovementsResponse
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro ao carregar insights.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  quick('7d')
})
</script>
