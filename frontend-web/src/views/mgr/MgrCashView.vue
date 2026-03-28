<template>
  <div class="page">
    <h1>Sessão de caixa</h1>
    <p v-if="err" class="err">{{ err }}</p>
    <template v-else-if="data">
      <template v-if="!data.open">
        <button type="button" class="btn-primary" aria-label="Abrir caixa" @click="open">Abrir caixa</button>
      </template>
      <template v-else>
        <p>Esperado: R$ {{ data.open.expected_amount ?? data.open.expectedAmount }}</p>
        <div class="field">
          <label for="act">Valor contado</label>
          <input id="act" v-model="actual" type="text" inputmode="decimal" />
        </div>
        <button type="button" class="btn-primary" aria-label="Fechar caixa" @click="close">Fechar caixa</button>
      </template>
    </template>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/gestor')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
const api = inject<AxiosInstance>('api')!
const err = ref('')
const data = ref<{
  open: {
    session_id?: string
    sessionId?: string
    expected_amount?: string
    expectedAmount?: string
  } | null
} | null>(null)
const actual = ref('')

async function refresh(): Promise<void> {
  err.value = ''
  try {
    const { data: d } = await api.get<typeof data.value>('/cash')
    data.value = d
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro.'
  }
}

async function open(): Promise<void> {
  try {
    await api.post('/cash/open', {})
    await refresh()
  } catch {
    err.value = 'Falha ao abrir.'
  }
}

async function close(): Promise<void> {
  const o = data.value?.open
  if (!o) return
  try {
    const sid = o.session_id ?? o.sessionId
    if (!sid) return
    const { data: res } = await api.post<{ alert?: boolean }>('/cash/close', {
      sessionId: sid,
      actualAmount: Number(actual.value.replace(',', '.')),
    })
    await refresh()
    if (res.alert) alert('Alerta: divergência no caixa acima do limite.')
  } catch {
    err.value = 'Falha ao fechar.'
  }
}

onMounted(() => {
  void refresh()
})
</script>
