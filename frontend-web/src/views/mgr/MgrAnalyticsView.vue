<template>
  <div class="page">
    <h1>Análises e Tendências</h1>
    <div style="display: flex; gap: 0.5rem; align-items: end; flex-wrap: wrap; margin-bottom: 1rem">
      <div class="field">
        <label for="days">Janela (dias)</label>
        <input id="days" v-model.number="days" type="number" min="1" max="90" />
      </div>
      <button type="button" class="btn-primary" :disabled="loading" @click="load">Atualizar</button>
      <button type="button" class="btn-secondary" @click="$router.push('/gestor')">Voltar</button>
    </div>

    <p v-if="err" class="err">{{ err }}</p>
    <template v-if="data">
      <p>Receita total: {{ money(data.totals.revenue) }}</p>
      <p>Pagamentos: {{ data.totals.payments }}</p>
      <p>Check-outs: {{ data.totals.checkouts }}</p>

      <h2 style="margin-top: 1rem">Horários de pico (check-outs)</h2>
      <ul>
        <li v-for="h in data.peak_hours" :key="`peak-${h.hour}`">{{ hh(h.hour) }} — {{ h.checkouts }} check-outs</li>
      </ul>

      <h2 style="margin-top: 1rem">Ganhos por horário (UTC)</h2>
      <ul>
        <li v-for="g in data.gains_by_hour" :key="`gain-${g.hour}`">
          {{ hh(g.hour) }} — {{ money(g.amount) }} ({{ g.payments }} pagamentos)
        </li>
      </ul>

      <h2 style="margin-top: 1rem">Tendência por dia (UTC)</h2>
      <table style="width: 100%; border-collapse: collapse">
        <thead>
          <tr>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Dia</th>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Receita</th>
            <th style="text-align: left; border-bottom: 1px solid #ddd">Pagamentos</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in data.trend_by_day" :key="t.day">
            <td>{{ t.day }}</td>
            <td>{{ money(t.amount) }}</td>
            <td>{{ t.payments }}</td>
          </tr>
        </tbody>
      </table>
    </template>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'

type AnalyticsResponse = {
  days: number
  totals: { revenue: string; payments: number; checkouts: number }
  trend_by_day: Array<{ day: string; amount: string; payments: number }>
  gains_by_hour: Array<{ hour: number; amount: string; payments: number }>
  peak_hours: Array<{ hour: number; checkouts: number }>
}

const api = inject<AxiosInstance>('api')!
const data = ref<AnalyticsResponse | null>(null)
const err = ref('')
const loading = ref(false)
const days = ref(14)

function money(v: string): string {
  const n = Number(v)
  if (Number.isNaN(n)) return v
  return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

function hh(hour: number): string {
  return `${String(hour).padStart(2, '0')}:00`
}

async function load(): Promise<void> {
  loading.value = true
  err.value = ''
  try {
    const safeDays = Math.min(90, Math.max(1, Math.trunc(days.value || 14)))
    days.value = safeDays
    const { data: body } = await api.get('/manager/analytics', { params: { days: safeDays } })
    data.value = body as AnalyticsResponse
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro ao carregar análises.'
  } finally {
    loading.value = false
  }
}

onMounted(() => { void load() })
</script>
