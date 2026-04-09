<template>
  <div class="page">
    <h1>Cartão</h1>
    <p v-if="amount">Valor: R$ {{ amount }}</p>
    <p v-if="msg" class="err">{{ msg }}</p>
    <p v-if="waitingHosted" class="muted">{{ STRINGS.S27 }}</p>
    <button
      v-if="hostedUrl"
      type="button"
      class="btn-primary"
      :aria-label="STRINGS.B33"
      @click="openHostedAgain"
    >
      {{ STRINGS.B33 }}
    </button>
    <button
      type="button"
      class="btn-primary"
      aria-label="Confirmar"
      :disabled="!amount || waitingHosted"
      @click="pay"
    >
      Confirmar
    </button>
    <button type="button" style="margin-left: 0.5rem" aria-label="Voltar" @click="goBack">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { str } from '@/lib/apiDto'
import { interpretCardPayResponse, pollPaymentUntilTerminal } from '@/lib/cardPayResult'
import { STRINGS } from '@/strings'

const props = defineProps<{ paymentId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()
const amount = ref('')
const msg = ref('')
const waitingHosted = ref(false)
const hostedUrl = ref<string | null>(null)
let pollAbort: AbortController | null = null

const useSandboxCheckout = import.meta.env.DEV

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

onUnmounted(() => {
  pollAbort?.abort()
})

function openHostedAgain(): void {
  const u = hostedUrl.value
  if (u) window.open(u, '_blank', 'noopener,noreferrer')?.focus()
}

function goBack(): void {
  pollAbort?.abort()
  void router.back()
}

async function pay(): Promise<void> {
  msg.value = ''
  hostedUrl.value = null
  pollAbort?.abort()
  try {
    const dec = Number(amount.value.replace(',', '.'))
    const { data } = await api.post<Record<string, unknown>>('/payments/card', {
      paymentId: props.paymentId,
      amount: dec,
    })
    const parsed = interpretCardPayResponse(data, useSandboxCheckout)
    if (parsed.kind === 'sync_paid') {
      alert(STRINGS.T4)
      await router.replace('/operador')
      return
    }
    if (parsed.kind === 'hosted_checkout') {
      hostedUrl.value = parsed.openUrl
      window.open(parsed.openUrl, '_blank', 'noopener,noreferrer')?.focus()
      waitingHosted.value = true
      pollAbort = new AbortController()
      try {
        const end = await pollPaymentUntilTerminal(
          async () => {
            const g = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
            return str(g.data.status) ?? 'PENDING'
          },
          { intervalMs: 2000, maxWaitMs: 900_000, signal: pollAbort.signal },
        )
        if (end === 'paid') {
          alert(STRINGS.T4)
          await router.replace('/operador')
          return
        }
        if (end === 'failed') {
          msg.value = 'Pagamento recusado ou falhou. Tente outro método.'
          return
        }
        if (end === 'expired') {
          msg.value = 'Pagamento expirado. Gere um novo checkout.'
          return
        }
        msg.value = STRINGS.S28
      } catch (e: unknown) {
        if (e instanceof DOMException && e.name === 'AbortError') return
        msg.value = 'Erro ao aguardar confirmação.'
      } finally {
        waitingHosted.value = false
      }
      return
    }
    msg.value = 'Resposta do servidor não reconhecida para cartão.'
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

<style scoped>
.muted {
  color: #555;
  max-width: 28rem;
  margin: 0.75rem 0;
}
</style>
