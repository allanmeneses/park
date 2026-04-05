<template>
  <div class="page">
    <h1>Checkout</h1>
    <p v-if="msg" class="err">{{ msg }}</p>
    <p v-else>Processando…</p>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { str } from '@/lib/apiDto'
import { checkoutZeroPaySummaryLines } from '@/lib/checkoutZeroPaySummary'
import { isZeroMoneyAmount } from '@/lib/moneyParse'

const props = defineProps<{ ticketId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()
const msg = ref('')

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.post<Record<string, unknown>>(
        `/tickets/${props.ticketId}/checkout`,
        {},
        { headers: { 'Idempotency-Key': crypto.randomUUID() } },
      )
      const amountRaw = data.amount ?? data.Amount ?? '0'
      const hoursTotal = Number(data.hours_total ?? data.hoursTotal ?? 0)
      const hoursCliente = Number(data.hours_cliente ?? data.hoursCliente ?? 0)
      const hoursLojista = Number(data.hours_lojista ?? data.hoursLojista ?? 0)
      if (isZeroMoneyAmount(amountRaw)) {
        const parts = checkoutZeroPaySummaryLines(hoursTotal, hoursLojista, hoursCliente)
        alert(parts.join(' '))
        await router.replace('/operador')
        return
      }
      const pid = str(data.payment_id ?? data.paymentId)
      if (!pid) {
        msg.value = 'Resposta sem pagamento.'
        return
      }
      await router.replace(`/operador/pagar/${pid}`)
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) {
        const code = (e.response?.data as { code?: string } | undefined)?.code
        if (code === 'INVALID_TICKET_STATE') {
          alert('Não foi possível registrar a saída neste estado.')
          await router.replace(`/operador/ticket/${props.ticketId}`)
          return
        }
        msg.value = apiErrorMessage(e.response?.data)
        return
      }
      msg.value = 'Erro.'
    }
  })()
})
</script>
