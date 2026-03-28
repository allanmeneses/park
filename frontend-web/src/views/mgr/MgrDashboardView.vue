<template>
  <div class="page">
    <h1>Painel</h1>
    <p v-if="err" class="err">{{ err }}</p>
    <template v-else-if="d">
      <p>Faturamento (hoje): {{ money(d.faturamento) }}</p>
      <p>Ocupação: {{ (Number(d.ocupacao) * 100).toFixed(1) }}%</p>
      <p>Check-outs hoje: {{ d.tickets_dia }}</p>
      <p>Uso convênio: {{ d.uso_convenio == null ? '—' : `${(Number(d.uso_convenio) * 100).toFixed(1)}%` }}</p>
    </template>
    <div style="margin-top: 1rem; display: flex; gap: 0.5rem">
      <button type="button" class="btn-primary" aria-label="Caixa" @click="$router.push('/gestor/caixa')">Caixa</button>
      <button type="button" class="btn-primary" aria-label="Configurações" @click="$router.push('/gestor/config')">
        Configurações
      </button>
      <button type="button" class="btn-primary" aria-label="Operação" @click="$router.push('/operador')">Operação</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'

const api = inject<AxiosInstance>('api')!
const d = ref<{
  faturamento: number
  ocupacao: number
  tickets_dia: number
  uso_convenio: number | null
} | null>(null)
const err = ref('')

function money(n: number): string {
  return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get('/dashboard')
      d.value = data as typeof d.value
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
      else err.value = 'Erro.'
    }
  })()
})
</script>
