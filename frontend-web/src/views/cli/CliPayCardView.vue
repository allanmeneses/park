<template>
  <div class="page">
    <h1>Cartão</h1>
    <p v-if="amount">Valor: R$ {{ amount }}</p>
    <p v-if="msg" class="err">{{ msg }}</p>
    <p v-if="loadingBrick" class="muted">Carregando formulário seguro do Mercado Pago...</p>
    <p v-else-if="waitingPayment" class="muted">
      Pagamento enviado. Esta tela acompanha a confirmação automaticamente.
    </p>
    <div :id="brickContainerId" class="brick-host" />
    <button type="button" style="margin-top: 1rem" aria-label="Carteira" @click="goBack">Carteira</button>
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
import {
  loadMercadoPagoSdk,
  type CardPaymentBrickController,
  type CardPaymentBrickFormData,
} from '@/lib/mercadoPagoBrick'

const props = defineProps<{ paymentId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()

const brickContainerId = 'cli-card-payment-brick'
const amount = ref('')
const msg = ref('')
const loadingBrick = ref(false)
const waitingPayment = ref(false)
const minMercadoPagoCardAmount = 1
let brickController: CardPaymentBrickController | null = null
let pollAbort: AbortController | null = null

function decimalAmount(): number {
  return Number(amount.value.replace(',', '.'))
}

function destroyBrick(): void {
  brickController?.unmount()
  brickController = null
}

async function startPolling(): Promise<void> {
  pollAbort?.abort()
  pollAbort = new AbortController()
  waitingPayment.value = true
  try {
    const end = await pollPaymentUntilTerminal(
      async () => {
        const g = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
        return str(g.data.status) ?? 'PENDING'
      },
      { intervalMs: 1000, maxWaitMs: 900_000, signal: pollAbort.signal },
    )
    if (end === 'paid') {
      await router.replace('/cliente')
      return
    }
    if (end === 'failed') {
      msg.value = 'Pagamento recusado ou falhou. Tente outro cartão ou use PIX.'
      return
    }
    if (end === 'expired') {
      msg.value = 'Pagamento expirado. Gere uma nova tentativa.'
      return
    }
    msg.value = 'Ainda não há confirmação do pagamento.'
  } catch (e: unknown) {
    if (e instanceof DOMException && e.name === 'AbortError') return
    msg.value = 'Erro ao aguardar confirmação do pagamento.'
  } finally {
    waitingPayment.value = false
  }
}

async function submitCard(formData: CardPaymentBrickFormData): Promise<void> {
  const dec = decimalAmount()
  const { data } = await api.post<Record<string, unknown>>('/payments/card', {
    paymentId: props.paymentId,
    amount: dec,
    flow: 'EMBEDDED',
    token: str(formData.token) ?? '',
    installments: Number(formData.installments ?? 1),
    paymentMethodId: str(formData.payment_method_id) ?? '',
    issuerId: formData.issuer_id == null ? null : String(formData.issuer_id),
    payerEmail: str(formData.payer?.email) ?? '',
    identificationType: str(formData.payer?.identification?.type) ?? null,
    identificationNumber: str(formData.payer?.identification?.number) ?? null,
  })

  const parsed = interpretCardPayResponse(data, false)
  if (parsed.kind === 'sync_paid') {
    await router.replace('/cliente')
    return
  }
  if (parsed.kind === 'failed_status') {
    throw new Error(parsed.message)
  }
  void startPolling()
}

async function renderBrick(publicKey: string): Promise<void> {
  destroyBrick()
  loadingBrick.value = true
  msg.value = ''
  const MercadoPago = await loadMercadoPagoSdk()
  const mp = new MercadoPago(publicKey, { locale: 'pt-BR' })
  const bricksBuilder = mp.bricks()
  brickController = await bricksBuilder.create('cardPayment', brickContainerId, {
    initialization: { amount: decimalAmount() },
    callbacks: {
      onReady: () => {
        loadingBrick.value = false
      },
      onSubmit: async (formData) => {
        msg.value = ''
        try {
          await submitCard(formData)
        } catch (e: unknown) {
          if (e instanceof Error && e.message) msg.value = e.message
          else msg.value = 'Não foi possível processar o cartão.'
          throw e
        }
      },
      onError: () => {
        loadingBrick.value = false
        msg.value = 'Não foi possível carregar o formulário de cartão.'
      },
    },
  })
}

async function loadEmbeddedSession(): Promise<void> {
  const { data: payment } = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
  amount.value = str(payment.amount) ?? ''
  const currentStatus = (str(payment.status) ?? '').toUpperCase()
  if (currentStatus === 'PAID') {
    await router.replace('/cliente')
    return
  }

  const dec = decimalAmount()
  if (dec < minMercadoPagoCardAmount) {
    msg.value = 'Pagamento com cartão disponível apenas para valores a partir de R$ 1,00. Para este pacote, volte e use PIX.'
    return
  }

  const { data } = await api.post<Record<string, unknown>>('/payments/card', {
    paymentId: props.paymentId,
    amount: dec,
    flow: 'EMBEDDED',
  })
  const parsed = interpretCardPayResponse(data, false)
  if (parsed.kind === 'embedded_bricks') {
    if (parsed.provider !== 'mercadopago' || !parsed.publicKey) {
      msg.value = 'Cartão embutido requer Mercado Pago configurado no servidor.'
      return
    }
    await renderBrick(parsed.publicKey)
    return
  }
  if (parsed.kind === 'sync_paid') {
    await router.replace('/cliente')
    return
  }
  if (parsed.kind === 'failed_status') {
    msg.value = parsed.message
    return
  }
  if (parsed.kind === 'pending_status') {
    await startPolling()
    return
  }
  msg.value = 'Resposta do servidor não reconhecida para cartão.'
}

function goBack(): void {
  pollAbort?.abort()
  void router.replace('/cliente')
}

onMounted(() => {
  void (async () => {
    try {
      await loadEmbeddedSession()
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) msg.value = apiErrorMessage(e.response?.data)
      else msg.value = 'Não foi possível carregar o pagamento com cartão.'
    }
  })()
})

onUnmounted(() => {
  pollAbort?.abort()
  destroyBrick()
})
</script>

<style scoped>
.muted {
  color: #555;
  max-width: 34rem;
}

.brick-host {
  margin-top: 1rem;
  min-height: 12rem;
}
</style>
