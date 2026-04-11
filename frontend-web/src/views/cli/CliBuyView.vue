<template>
  <div class="page">
    <h1>Comprar horas</h1>
    <p v-if="!pkgs.length" class="empty">Nenhum pacote disponível no momento.</p>
    <ul class="pkg-list">
      <li v-for="p in pkgs" :key="p.id" class="pkg-item">
        <button type="button" class="btn-primary pkg-button" aria-label="Selecionar" @click="pick(p)">
          <span class="pkg-title">
            <strong>{{ p.display_name || `${p.hours} h` }}</strong>
            <span v-if="p.is_promo" class="pkg-badge">Promocional</span>
          </span>
          <span class="pkg-subtitle">{{ p.hours }} h — R$ {{ p.price }}</span>
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
import { compareRechargePackages, rechargePackageFromApi, str, type RechargePackageDto } from '@/lib/apiDto'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const pkgs = ref<RechargePackageDto[]>([])

onMounted(() => {
  void (async () => {
    const { data } = await api.get<{ items: Record<string, unknown>[] }>('/recharge-packages?scope=CLIENT')
    pkgs.value = (data.items ?? []).map(rechargePackageFromApi).sort(compareRechargePackages)
  })()
})

async function pick(p: RechargePackageDto): Promise<void> {
  const m = confirm('Usar crédito interno? Cancelar segue para o pagamento via PIX.')
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

<style scoped>
.pkg-list {
  list-style: none;
  padding: 0;
}

.pkg-item {
  margin-bottom: 0.6rem;
}

.empty {
  color: #555;
}

.pkg-button {
  width: 100%;
  text-align: left;
}

.pkg-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.pkg-subtitle {
  display: block;
  margin-top: 0.2rem;
  font-size: 0.95rem;
}

.pkg-badge {
  display: inline-block;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
  background: #fff3cd;
  color: #7a5600;
  font-size: 0.8rem;
  font-weight: 600;
}
</style>
