<template>
  <div class="page">
    <h1>{{ STRINGS.B32 }}</h1>
    <p class="sub">
      Saldo de convênio por lojista, horas bonificadas por placa (só saldo &gt; 0) e crédito comprado por placa. Listas de
      placa ordenadas por maior saldo primeiro.
    </p>
    <div class="filter-row">
      <PlateField
        id="plate-filter"
        v-model="plateFilter"
        label="Filtrar placa"
        class="field"
        placeholder="ABC-1D23"
        aria-label="Filtrar placa"
        @submit="load"
      />
      <button type="button" class="btn-secondary" :disabled="loading" @click="load">Atualizar</button>
    </div>
    <p v-if="err" class="err">{{ err }}</p>
    <p v-else-if="loading">Carregando…</p>
    <template v-else-if="data">
      <h2 class="section-title">Lojistas — saldo convênio (h)</h2>
      <ul v-if="data.lojistas.length" class="list">
        <li v-for="(r, i) in data.lojistas" :key="r.lojistaId || i">
          <strong>{{ r.lojistaName || '—' }}</strong> — {{ r.balanceHours }} h
        </li>
      </ul>
      <p v-else class="muted">Nenhum lojista com carteira registada.</p>

      <h2 class="section-title">Placas — bonificação lojista disponível (h)</h2>
      <ul v-if="data.lojistaBonificadoPlates.length" class="list">
        <li v-for="(r, i) in data.lojistaBonificadoPlates" :key="r.plate + i">
          <strong>{{ r.plate }}</strong> — {{ r.balanceHours }} h
        </li>
      </ul>
      <p v-else class="muted">Nenhuma placa com bonificação de lojista disponível (com o filtro atual).</p>

      <h2 class="section-title">Clientes — crédito comprado por placa (h)</h2>
      <ul v-if="data.clientPlates.length" class="list">
        <li v-for="(r, i) in data.clientPlates" :key="r.plate + i">
          <strong>{{ r.plate }}</strong> — {{ r.balanceHours }} h
          <span v-if="r.expirationDate" class="muted"> (validade {{ r.expirationDate }})</span>
        </li>
      </ul>
      <p v-else class="muted">Nenhum cliente com o filtro atual.</p>
    </template>
    <button type="button" class="btn-secondary" style="margin-top: 1rem" @click="$router.push('/gestor')">
      Voltar ao painel
    </button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import PlateField from '@/components/PlateField.vue'
import { apiErrorMessage } from '@/lib/errors'
import { normalizePlate } from '@/lib/plate'
import { parseBalancesReportPayload, type BalancesReportPayload } from '@/lib/balancesReport'
import { STRINGS } from '@/strings'

const api = inject<AxiosInstance>('api')!
const plateFilter = ref('')
const data = ref<BalancesReportPayload | null>(null)
const err = ref('')
const loading = ref(false)

async function load(): Promise<void> {
  loading.value = true
  err.value = ''
  try {
    const params: Record<string, string> = {}
    const p = normalizePlate(plateFilter.value)
    if (p) params.plate = p
    const { data: raw } = await api.get<unknown>('/manager/balances-report', { params })
    data.value = parseBalancesReportPayload(raw)
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro.'
    data.value = null
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void load()
})
</script>

<style scoped>
.sub {
  opacity: 0.85;
  margin-bottom: 1rem;
}
.filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: flex-end;
  margin-bottom: 1rem;
}
.filter-row label {
  width: 100%;
  font-size: 0.9rem;
}
.field {
  padding: 0.5rem 0.75rem;
  min-width: 12rem;
}
.section-title {
  font-size: 1.05rem;
  margin: 1.25rem 0 0.5rem;
}
.list {
  margin: 0;
  padding-left: 1.25rem;
}
.muted {
  opacity: 0.8;
  font-size: 0.9rem;
}
</style>
