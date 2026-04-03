<template>
  <div class="page">
    <h1>Painel</h1>
    <div style="margin-bottom: 0.75rem; display: flex; gap: 0.5rem; flex-wrap: wrap">
      <button type="button" class="btn-secondary" :disabled="loading" @click="load('today')">Hoje (UTC)</button>
      <button type="button" class="btn-secondary" :disabled="loading" @click="load('24h')">Últimas 24h</button>
    </div>
    <p v-if="d">Visualização: {{ d.view === '24h' ? 'Últimas 24h' : 'Hoje (UTC)' }}</p>
    <p v-if="err" class="err">{{ err }}</p>
    <template v-else-if="d">
      <p>Faturamento (hoje): {{ money(d.faturamento) }}</p>
      <p>Ocupação: {{ (Number(d.ocupacao) * 100).toFixed(1) }}%</p>
      <p>Check-outs hoje: {{ d.tickets_dia }}</p>
      <p>Uso convênio: {{ d.uso_convenio == null ? '—' : `${(Number(d.uso_convenio) * 100).toFixed(1)}%` }}</p>
    </template>
    <div style="margin-top: 1rem; display: flex; gap: 0.5rem; flex-wrap: wrap">
      <button type="button" class="btn-primary" aria-label="Insights" @click="$router.push('/gestor/movimentos')">
        Insights
      </button>
      <button type="button" class="btn-primary" aria-label="Análises" @click="$router.push('/gestor/analises')">
        Análises
      </button>
      <button type="button" class="btn-primary" aria-label="Caixa" @click="$router.push('/gestor/caixa')">Caixa</button>
      <button
        v-if="canManageLojistaInvites"
        type="button"
        class="btn-primary"
        :aria-label="STRINGS.B26"
        @click="$router.push('/gestor/lojista-convites')"
      >
        {{ STRINGS.B26 }}
      </button>
      <button type="button" class="btn-primary" aria-label="Configurações" @click="$router.push('/gestor/config')">
        Configurações
      </button>
      <button type="button" class="btn-primary" aria-label="Operação" @click="$router.push('/operador')">Operação</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { STRINGS } from '@/strings'
import { useAuthStore } from '@/stores/auth'

const api = inject<AxiosInstance>('api')!
const auth = useAuthStore()
const canManageLojistaInvites = computed(
  () => auth.role === 'ADMIN' || auth.role === 'SUPER_ADMIN',
)
const d = ref<{
  faturamento: number
  ocupacao: number
  tickets_dia: number
  uso_convenio: number | null
  view: 'today' | '24h'
} | null>(null)
const err = ref('')
const loading = ref(false)

function money(n: number): string {
  return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

async function load(view: 'today' | '24h'): Promise<void> {
  loading.value = true
  err.value = ''
  try {
    const { data } = await api.get('/dashboard', { params: view === '24h' ? { view: '24h' } : {} })
    d.value = data as typeof d.value
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro.'
  } finally {
    loading.value = false
  }
}

onMounted(() => { void load('today') })
</script>
