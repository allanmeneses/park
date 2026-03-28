<template>
  <div class="page">
    <h1>Carteira de convênio</h1>
    <p v-if="err" class="err">{{ err }}</p>
    <p v-else>Saldo: {{ bal }} horas</p>
    <button type="button" class="btn-primary" style="margin-top: 1rem" aria-label="Comprar horas" @click="$router.push('/lojista/comprar')">
      Comprar horas
    </button>
    <button type="button" class="btn-primary" aria-label="Histórico" style="margin-left: 0.5rem" @click="$router.push('/lojista/historico')">
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
const err = ref('')

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get<{ balance_hours?: number }>('/lojista/wallet')
      bal.value = data.balance_hours ?? 0
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
      else err.value = 'Erro.'
    }
  })()
})
</script>
