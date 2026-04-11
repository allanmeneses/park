<template>
  <div class="page">
    <h1>Comprar horas (convênio)</h1>
    <p v-if="!pkgs.length" class="empty">Nenhum pacote disponível no momento.</p>
    <ul class="pkg-list">
      <li v-for="p in pkgs" :key="p.id" class="pkg-item">
        <button
          type="button"
          class="btn-primary pkg-button"
          :class="{ selected: selectedPkg?.id === p.id }"
          :aria-label="selectedPkg?.id === p.id ? 'Pacote selecionado' : 'Selecionar'"
          :aria-pressed="selectedPkg?.id === p.id"
          @click="selectPackage(p)"
        >
          <span class="pkg-title">
            <strong>{{ p.display_name || `${p.hours} h` }}</strong>
            <span v-if="p.is_promo" class="pkg-badge">Promocional</span>
            <span v-if="selectedPkg?.id === p.id" class="pkg-badge selected-badge">Selecionado</span>
          </span>
          <span class="pkg-subtitle">{{ p.hours }} h — R$ {{ p.price }}</span>
        </button>
      </li>
    </ul>
    <section v-if="selectedPkg" class="pay-panel" aria-labelledby="pay-panel-title">
      <h2 id="pay-panel-title">Forma de pagamento</h2>
      <p>
        <strong>{{ selectedPkg.display_name || `${selectedPkg.hours} h` }}</strong>
        — {{ selectedPkg.hours }} h — R$ {{ selectedPkg.price }}
      </p>
      <button type="button" class="btn-primary" aria-label="Pagar com PIX" @click="payPix">
        Pagar com PIX
      </button>
      <button
        type="button"
        class="btn-secondary"
        aria-label="Pagar com cartão em breve"
        disabled
        style="margin-left: 0.5rem"
      >
        Pagar com cartão (em breve)
      </button>
      <p class="hint">O pagamento com cartão ainda não está disponível para esta compra.</p>
    </section>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/lojista')">Voltar</button>
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
const selectedPkg = ref<RechargePackageDto | null>(null)

onMounted(() => {
  void (async () => {
    const { data } = await api.get<{ items: Record<string, unknown>[] }>('/recharge-packages?scope=LOJISTA')
    pkgs.value = (data.items ?? []).map(rechargePackageFromApi).sort(compareRechargePackages)
  })()
})

function selectPackage(p: RechargePackageDto): void {
  selectedPkg.value = p
}

async function payPix(): Promise<void> {
  if (!selectedPkg.value) return
  const { data } = await api.post<Record<string, unknown>>(
    '/lojista/buy',
    { packageId: selectedPkg.value.id, settlement: 'PIX' },
    { headers: { 'Idempotency-Key': crypto.randomUUID() } },
  )
  const pid = str(data.payment_id ?? data.paymentId)
  if (pid) await router.push(`/lojista/pix/${pid}`)
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

.pkg-button.selected {
  border: 2px solid #1976d2;
  background: #eaf3ff;
  color: #0d47a1;
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

.selected-badge {
  background: #dbeafe;
  color: #0d47a1;
}

.pay-panel {
  margin-top: 1rem;
  padding: 0.85rem;
  border: 1px solid #ddd;
  border-radius: 0.5rem;
  max-width: 32rem;
}

.pay-panel h2 {
  margin-top: 0;
  font-size: 1rem;
}

.hint {
  margin-top: 0.6rem;
  color: #666;
  font-size: 0.92rem;
}
</style>
