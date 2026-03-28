<template>
  <div class="page">
    <h1>Pagamento</h1>
    <p v-if="cashOpen === null">Verificando caixa…</p>
    <template v-else>
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
    </template>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.back()">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
const props = defineProps<{ paymentId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()
const cashOpen = ref<boolean | null>(null)

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get<{ open?: unknown }>('/cash')
      cashOpen.value = data.open != null
    } catch {
      cashOpen.value = false
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
