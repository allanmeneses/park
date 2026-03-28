<template>
  <div class="page">
    <h1>Cartão</h1>
    <p v-if="amount">Valor: R$ {{ amount }}</p>
    <p v-if="msg" class="err">{{ msg }}</p>
    <button type="button" class="btn-primary" aria-label="Confirmar" :disabled="!amount" @click="pay">Confirmar</button>
    <button type="button" style="margin-left: 0.5rem" aria-label="Voltar" @click="$router.back()">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { str } from '@/lib/apiDto'

const props = defineProps<{ paymentId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()
const amount = ref('')
const msg = ref('')

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
      amount.value = str(data.amount ?? data.Amount)
    } catch {
      msg.value = 'Não foi possível carregar o pagamento.'
    }
  })()
})

async function pay(): Promise<void> {
  msg.value = ''
  try {
    const dec = Number(amount.value.replace(',', '.'))
    await api.post('/payments/card', { paymentId: props.paymentId, amount: dec })
    alert('Pagamento confirmado.')
    await router.replace('/operador')
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) {
      const code = (e.response?.data as { code?: string } | undefined)?.code
      if (code === 'AMOUNT_MISMATCH') {
        msg.value = 'Valor enviado não confere com o ticket.'
        return
      }
      msg.value = apiErrorMessage(e.response?.data)
      return
    }
    msg.value = 'Erro.'
  }
}
</script>
