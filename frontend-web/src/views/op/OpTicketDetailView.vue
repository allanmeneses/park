<template>
  <div class="page">
    <h1>Ticket</h1>
    <p v-if="state === 'loading'">Carregando…</p>
    <p v-else-if="state === 'error'" class="err">{{ err }}</p>
    <template v-else-if="ticket">
      <p><strong>Placa:</strong> {{ ticket.plate }}</p>
      <p><strong>Status:</strong> {{ ticket.status }}</p>
      <p><strong>Entrada:</strong> {{ ticket.entry_time }}</p>
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
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { str } from '@/lib/apiDto'

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
    state.value = 'ready'
  } catch (e: unknown) {
    state.value = 'error'
    if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
    else err.value = 'Erro.'
  }
}

onMounted(() => {
  void load()
})
</script>
