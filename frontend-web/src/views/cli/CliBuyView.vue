<template>
  <div class="page">
    <h1>Comprar horas</h1>
    <ul style="list-style: none; padding: 0">
      <li v-for="p in pkgs" :key="String(p.id)" style="margin-bottom: 0.5rem">
        <button type="button" class="btn-primary" aria-label="Selecionar" @click="pick(p)">
          {{ p.hours }} h — R$ {{ p.price }}
        </button>
      </li>
    </ul>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/cliente')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import { str } from '@/lib/apiDto'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const pkgs = ref<{ id: string; hours: number; price: string }[]>([])

onMounted(() => {
  void (async () => {
    const { data } = await api.get<{ items: Record<string, unknown>[] }>('/recharge-packages?scope=CLIENT')
    pkgs.value = (data.items ?? []).map((x) => ({
      id: str(x.id),
      hours: Number(x.hours),
      price: str(x.price),
    }))
  })()
})

async function pick(p: { id: string; hours: number; price: string }): Promise<void> {
  const m = confirm('Crédito (OK) ou cancelar para PIX via próxima etapa não implementada neste diálogo.')
  if (m) {
    await api.post(
      '/client/buy',
      { packageId: p.id, settlement: 'CREDIT' },
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    )
    alert('Compra concluída.')
    await router.push('/cliente')
    return
  }
  const { data } = await api.post<Record<string, unknown>>(
    '/client/buy',
    { packageId: p.id, settlement: 'PIX' },
    { headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  const pid = str(data.payment_id ?? data.paymentId)
  if (pid) await router.push(`/cliente/pix/${pid}`)
}
</script>
