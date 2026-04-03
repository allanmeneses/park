<template>
  <div class="page">
    <h1>Ticket</h1>
    <p v-if="state === 'loading'">Carregando…</p>
    <p v-else-if="state === 'error'" class="err">{{ err }}</p>
    <template v-else-if="ticket">
      <p><strong>Placa:</strong> {{ ticket.plate }}</p>
      <p><strong>Status:</strong> {{ ticket.status }}</p>
      <p><strong>Entrada:</strong> {{ ticket.entry_time }}</p>
      <p v-if="elapsedLabel != null">
        <strong>Tempo decorrido:</strong> {{ elapsedLabel }}
        <span v-if="ticket.status === 'OPEN'" style="opacity: 0.75; font-size: 0.9rem">(ao vivo)</span>
        <span
          v-else-if="ticket.status === 'AWAITING_PAYMENT'"
          style="opacity: 0.75; font-size: 0.9rem"
        >
          (ao vivo — pagamento pendente; o valor da fatura usa a saída indicada abaixo)
        </span>
      </p>
      <p v-if="ticket.exit_time"><strong>Saída:</strong> {{ ticket.exit_time }}</p>
      <template v-if="ticket.status === 'OPEN'">
        <button type="button" class="btn-primary" aria-label="Registrar saída (checkout)" @click="goCheckout">
          Registrar saída (checkout)
        </button>
      </template>
      <template v-else-if="ticket.status === 'AWAITING_PAYMENT' && paymentId">
        <button
          type="button"
          class="btn-primary"
          aria-label="Pagar"
          @click="$router.push(`/operador/pagar/${paymentId}`)"
        >
          Pagar
        </button>
      </template>
      <p v-else-if="ticket.status === 'CLOSED'">Ticket encerrado.</p>
    </template>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/operador')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { str } from '@/lib/apiDto'
import { elapsedWholeSeconds, formatElapsedPtBr } from '@/lib/elapsedRealtime'

const props = defineProps<{ id: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()

const state = ref<'loading' | 'ready' | 'error'>('loading')
const err = ref('')
const ticket = ref<{
  plate: string
  status: string
  entry_time: string
  exit_time?: string | null
} | null>(null)
const paymentId = ref<string | null>(null)
const elapsedLabel = ref<string | null>(null)
let elapsedIntervalId: ReturnType<typeof setInterval> | undefined

/** Enquanto não está encerrado nem pago: o relógio segue (inclui voltar da tela de pagar). */
function usesLiveElapsedClock(status: string): boolean {
  return status === 'OPEN' || status === 'AWAITING_PAYMENT'
}

function stopLiveElapsedTick(): void {
  if (elapsedIntervalId != null) {
    clearInterval(elapsedIntervalId)
    elapsedIntervalId = undefined
  }
}

/** OPEN e AWAITING_PAYMENT: intervalo 1 s; pausa com aba em segundo plano. */
function restartLiveElapsedTick(): void {
  stopLiveElapsedTick()
  recalcElapsed()
  if (!ticket.value || !usesLiveElapsedClock(ticket.value.status) || document.visibilityState !== 'visible')
    return
  elapsedIntervalId = setInterval(() => {
    recalcElapsed()
  }, 1000)
}

function onVisibilityChange(): void {
  if (document.visibilityState === 'visible') {
    recalcElapsed()
    restartLiveElapsedTick()
  } else {
    stopLiveElapsedTick()
  }
}

function recalcElapsed(): void {
  const t = ticket.value
  if (!t) {
    elapsedLabel.value = null
    return
  }
  const entryMs = Date.parse(t.entry_time)
  if (Number.isNaN(entryMs)) {
    elapsedLabel.value = null
    return
  }
  const entry = new Date(entryMs)
  if (usesLiveElapsedClock(t.status)) {
    const sec = elapsedWholeSeconds(entry, new Date())
    elapsedLabel.value = formatElapsedPtBr(sec)
    return
  }
  if (t.exit_time) {
    const exitMs = Date.parse(t.exit_time)
    if (!Number.isNaN(exitMs)) {
      const sec = elapsedWholeSeconds(entry, new Date(exitMs))
      elapsedLabel.value = formatElapsedPtBr(sec)
      return
    }
  }
  elapsedLabel.value = null
}

function goCheckout(): void {
  void router.push(`/operador/checkout/${props.id}`)
}

async function load(): Promise<void> {
  state.value = 'loading'
  try {
    const { data } = await api.get<{
      ticket: Record<string, unknown>
      payment?: Record<string, unknown> | null
    }>(`/tickets/${props.id}`)
    const tr = data.ticket
    ticket.value = {
      plate: str(tr.plate ?? tr.Plate),
      status: str(tr.status ?? tr.Status),
      entry_time: str(tr.entryTime ?? tr.entry_time),
      exit_time: (tr.exitTime ?? tr.exit_time) as string | null | undefined,
    }
    const pay = data.payment
    paymentId.value = pay ? str(pay.id ?? pay.Id) || null : null
    restartLiveElapsedTick()
    state.value = 'ready'
  } catch (e: unknown) {
    state.value = 'error'
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro.'
  }
}

watch(
  () => props.id,
  () => {
    void load()
  },
)

onMounted(() => {
  document.addEventListener('visibilitychange', onVisibilityChange)
  void load()
})

onUnmounted(() => {
  document.removeEventListener('visibilitychange', onVisibilityChange)
  stopLiveElapsedTick()
})
</script>
