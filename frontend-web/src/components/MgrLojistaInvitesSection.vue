<template>
  <section class="loj-invites">
    <h2 class="h2">Convites — Lojista</h2>
    <p class="sub">
      Gere um par de códigos para passar ao lojista antes do cadastro. O código de ativação só é exibido uma vez.
    </p>
    <div class="field">
      <label for="inviteDisplay">Nome exibido (opcional)</label>
      <input id="inviteDisplay" v-model="inviteDisplayName" type="text" aria-label="Nome exibido do convite" />
    </div>
    <button
      type="button"
      class="btn-primary"
      aria-label="Gerar convite de lojista"
      :disabled="inviteLoading"
      @click="createInvite"
    >
      Gerar convite
    </button>
    <p v-if="inviteMsg" class="err">{{ inviteMsg }}</p>
    <div v-if="lastActivationPlain" class="invite-banner">
      <p><strong>Código do lojista:</strong> {{ lastMerchantCode }}</p>
      <p><strong>Código de ativação:</strong> {{ lastActivationPlain }}</p>
      <p class="sub">Copie agora — o código de ativação não poderá ser recuperado depois.</p>
    </div>
    <h3 class="h3">Lojistas do estacionamento</h3>
    <p class="sub">Todos os lojistas cadastrados (pendentes e com conta ativa). Horas e saldo só aparecem após ativação.</p>
    <p v-if="!lojInvites.length" class="sub">Nenhum lojista ainda.</p>
    <ul v-else class="loj-list">
      <li v-for="row in lojInvites" :key="row.lojistaId" class="loj-card">
        <div class="loj-head">
          <strong>{{ row.shopName || '—' }}</strong>
          <span class="badge">{{ row.activated ? 'Ativado' : 'Pendente' }}</span>
        </div>
        <p class="mono">
          <template v-if="row.merchantCode">Código: {{ row.merchantCode }}</template>
          <template v-else>Código público: —</template>
        </p>
        <template v-if="row.activated">
          <p v-if="row.email" class="sub">E-mail: {{ row.email }}</p>
          <p class="sub">
            Horas compradas: {{ row.totalPurchasedHours ?? 0 }} · Saldo disponível: {{ row.balanceHours ?? 0 }} h
          </p>
        </template>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { str } from '@/lib/apiDto'
import { apiErrorMessage } from '@/lib/errors'

export type LojistaInviteListRow = {
  merchantCode: string | null
  lojistaId: string
  shopName: string
  activated: boolean
  email: string | null
  totalPurchasedHours: number | null
  balanceHours: number | null
}

const api = inject<AxiosInstance>('api')!

const inviteDisplayName = ref('')
const inviteLoading = ref(false)
const inviteMsg = ref('')
const lastMerchantCode = ref('')
const lastActivationPlain = ref('')
const lojInvites = ref<LojistaInviteListRow[]>([])

function numOrNull(v: unknown): number | null {
  if (v == null) return null
  const n = Number(v)
  return Number.isFinite(n) ? n : null
}

async function loadLojInvites(): Promise<void> {
  try {
    const { data } = await api.get<{
      items?: Record<string, unknown>[]
    }>('/admin/lojista-invites')
    lojInvites.value = (data.items ?? []).map((x) => ({
      merchantCode: x.merchantCode != null ? String(x.merchantCode) : null,
      lojistaId: str(x.lojistaId ?? x.lojista_id),
      shopName: str(x.shopName ?? x.shop_name),
      activated: Boolean(x.activated),
      email: x.email != null ? String(x.email) : null,
      totalPurchasedHours: numOrNull(x.totalPurchasedHours ?? x.total_purchased_hours),
      balanceHours: numOrNull(x.balanceHours ?? x.balance_hours),
    }))
  } catch {
    /* ignore */
  }
}

async function createInvite(): Promise<void> {
  inviteMsg.value = ''
  lastMerchantCode.value = ''
  lastActivationPlain.value = ''
  inviteLoading.value = true
  try {
    const { data } = await api.post<Record<string, unknown>>('/admin/lojista-invites', {
      displayName: inviteDisplayName.value.trim() || null,
    })
    lastMerchantCode.value = str(data.merchantCode ?? data.merchant_code)
    lastActivationPlain.value = str(data.activationCode ?? data.activation_code)
    await loadLojInvites()
  } catch (e: unknown) {
    if (axios.isAxiosError(e))
      inviteMsg.value = apiErrorMessage(e.response?.data, 'Falha ao gerar convite.')
    else inviteMsg.value = 'Falha ao gerar convite.'
  } finally {
    inviteLoading.value = false
  }
}

onMounted(() => {
  void loadLojInvites()
})
</script>

<style scoped>
.h2 {
  margin-top: 0.5rem;
  font-size: 1.1rem;
}
.h3 {
  margin-top: 1rem;
  font-size: 1rem;
}
.sub {
  color: #757575;
  font-size: 0.9rem;
  margin: 0.5rem 0;
}
.invite-banner {
  margin-top: 1rem;
  padding: 0.75rem;
  background: #e3f2fd;
  border-radius: 4px;
}
.field {
  margin-bottom: 0.75rem;
}
.err {
  color: #c62828;
}
.loj-list {
  list-style: none;
  padding: 0;
  margin: 0.5rem 0 0;
}
.loj-card {
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  padding: 0.75rem;
  margin-bottom: 0.5rem;
}
.loj-head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.35rem;
}
.badge {
  font-size: 0.8rem;
  padding: 0.15rem 0.45rem;
  background: #f5f5f5;
  border-radius: 4px;
}
.mono {
  font-family: ui-monospace, monospace;
  font-size: 0.9rem;
  margin: 0.25rem 0;
}
</style>
