<template>
  <div class="page">
    <h1>Carteira</h1>
    <p v-if="err" class="err">{{ err }}</p>
    <template v-else>
      <p>Saldo: {{ bal }} horas</p>
      <p v-if="exp">Validade: {{ exp }}</p>
    </template>
    <button type="button" class="btn-primary" aria-label="Comprar horas" style="margin-top: 1rem" @click="$router.push('/cliente/comprar')">
      Comprar horas
    </button>
    <button type="button" class="btn-primary" aria-label="Histórico" style="margin-left: 0.5rem" @click="$router.push('/cliente/historico')">
      Histórico
    </button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'

const api = inject<AxiosInstance>('api')!
const bal = ref(0)
const exp = ref('')
const err = ref('')

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get<{ balance_hours?: number; expiration_date?: string | null }>('/client/wallet')
      bal.value = data.balance_hours ?? 0
      if (data.expiration_date) {
        exp.value = new Date(data.expiration_date).toLocaleDateString('pt-BR')
      }
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
      else err.value = 'Erro.'
    }
  })()
})
</script>
