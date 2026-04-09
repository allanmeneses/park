<template>
  <div class="page">
    <h1>Pagamento</h1>
    <p v-if="!screenReady">Carregando…</p>
    <template v-else>
      <p v-if="syncMsg" class="err">{{ syncMsg }}</p>
      <template v-if="!syncMsg">
        <button type="button" class="btn-primary" style="width: 100%; margin-bottom: 0.5rem" aria-label="PIX" @click="goPix">
          PIX
        </button>
        <button type="button" class="btn-primary" style="width: 100%; margin-bottom: 0.5rem" aria-label="Cartão" @click="goCard">
          Cartão
        </button>
        <button
          type="button"
          class="btn-primary"
          style="width: 100%; margin-bottom: 0.5rem"
          :disabled="!cashOpen"
          aria-label="Dinheiro"
          :title="!cashOpen ? 'Abra o caixa para habilitar dinheiro.' : ''"
          @click="cashPay"
        >
          Dinheiro
        </button>
        <p v-if="!cashOpen" style="opacity: 0.75; font-size: 0.9rem">Abra o caixa para habilitar dinheiro.</p>
      </template>
      <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.back()">Voltar</button>
    </template>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { str } from '@/lib/apiDto'
import { isZeroMoneyAmount } from '@/lib/moneyParse'
import { canIgnoreCheckoutRefreshError, refreshPendingCheckoutForTicket, ticketIdFromPaymentPayload } from '@/lib/parkingCheckoutSync'

const props = defineProps<{ paymentId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()
const screenReady = ref(false)
const cashOpen = ref(false)
const syncMsg = ref('')

function paymentSettled(pay: Record<string, unknown>): boolean {
  const st = str(pay.status ?? pay.Status).toUpperCase()
  if (st === 'PAID') return true
  return st === 'PENDING' && isZeroMoneyAmount(pay.amount ?? pay.Amount)
}

async function ticketClosed(apiClient: AxiosInstance, ticketId: string): Promise<boolean> {
  try {
    const { data } = await apiClient.get<{ ticket: Record<string, unknown> }>(`/tickets/${ticketId}`)
    return str(data.ticket.status ?? data.ticket.Status).toUpperCase() === 'CLOSED'
  } catch {
    return false
  }
}

onMounted(() => {
  void (async () => {
    try {
      try {
        const { data } = await api.get<{ open?: unknown }>('/cash')
        cashOpen.value = data.open != null
      } catch {
        cashOpen.value = false
      }

      let pay: Record<string, unknown>
      try {
        const r = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
        pay = r.data
      } catch (e: unknown) {
        if (axios.isAxiosError(e)) syncMsg.value = apiErrorMessage(e.response?.data)
        else syncMsg.value = 'Não foi possível carregar o pagamento.'
        return
      }

      if (paymentSettled(pay)) {
        alert('Saída registrada. Nada a pagar.')
        await router.replace('/operador')
        return
      }

      const tid = ticketIdFromPaymentPayload(pay)
      const pending = str(pay.status ?? pay.Status).toUpperCase() === 'PENDING'
      if (tid && pending) {
        try {
          await refreshPendingCheckoutForTicket(api, tid)
          const r2 = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
          pay = r2.data
          if (paymentSettled(pay)) {
            alert('Saída registrada. Nada a pagar.')
            await router.replace('/operador')
            return
          }
        } catch (e: unknown) {
          if (axios.isAxiosError(e) && e.response?.status === 409) {
            if (await ticketClosed(api, tid)) {
              alert('Saída registrada. Nada a pagar.')
              await router.replace('/operador')
              return
            }
            if (canIgnoreCheckoutRefreshError(e)) {
              const r2 = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
              pay = r2.data
              if (paymentSettled(pay)) {
                alert('Saída registrada. Nada a pagar.')
                await router.replace('/operador')
                return
              }
              // Conflito esperado em recalculo concorrente; não bloquear tela de escolha.
              continue
            }
          }
          if (axios.isAxiosError(e)) syncMsg.value = apiErrorMessage(e.response?.data)
          else syncMsg.value = 'Não foi possível atualizar saída/tempo para este pagamento.'
        }
      }
    } finally {
      screenReady.value = true
    }
  })()
})

function goPix(): void {
  void router.push(`/operador/pix/${props.paymentId}`)
}

function goCard(): void {
  void router.push(`/operador/cartao/${props.paymentId}`)
}

async function cashPay(): Promise<void> {
  if (!cashOpen.value) return
  if (!confirm('Confirmar recebimento em dinheiro neste valor?')) return
  try {
    await api.post('/payments/cash', { paymentId: props.paymentId })
    alert('Pagamento confirmado.')
    await router.replace('/operador')
  } catch {
    alert('Falha no pagamento.')
  }
}
</script>
