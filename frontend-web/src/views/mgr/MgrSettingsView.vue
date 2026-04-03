<template>
  <div class="page">
    <h1>Configurações</h1>
    <MgrLojistaInvitesSection v-if="showLojistaInvites" />
    <div class="field">
      <label for="price">Preço por hora</label>
      <input id="price" v-model="price" type="text" inputmode="decimal" />
    </div>
    <div class="field">
      <label for="cap">Capacidade</label>
      <input id="cap" v-model="capacity" type="number" min="1" />
    </div>
    <p v-if="msg" class="err">{{ msg }}</p>
    <button type="button" class="btn-primary" aria-label="Salvar" @click="save">Salvar</button>
    <h2 style="margin-top: 2rem; font-size: 1rem">Pacotes — Cliente</h2>
    <p v-if="!clientPkgs.length">Nenhum pacote cadastrado para este tipo.</p>
    <ul v-else>
      <li v-for="p in clientPkgs" :key="String(p.id)">{{ p.hours }} h — R$ {{ p.price }}</li>
    </ul>
    <h2 style="margin-top: 1rem; font-size: 1rem">Pacotes — Lojista</h2>
    <p v-if="!lojPkgs.length">Nenhum pacote cadastrado para este tipo.</p>
    <ul v-else>
      <li v-for="p in lojPkgs" :key="String(p.id)">{{ p.hours }} h — R$ {{ p.price }}</li>
    </ul>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/gestor')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { str } from '@/lib/apiDto'
import { useAuthStore } from '@/stores/auth'
import MgrLojistaInvitesSection from '@/components/MgrLojistaInvitesSection.vue'

const api = inject<AxiosInstance>('api')!
const auth = useAuthStore()
const price = ref('')
const capacity = ref('1')
const msg = ref('')
const clientPkgs = ref<{ id: unknown; hours: number; price: string }[]>([])
const lojPkgs = ref<{ id: unknown; hours: number; price: string }[]>([])

const showLojistaInvites = computed(
  () => auth.role === 'ADMIN' || auth.role === 'SUPER_ADMIN',
)

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get<Record<string, unknown>>('/settings')
      price.value = str(data.price_per_hour ?? data.pricePerHour)
      capacity.value = str(data.capacity ?? data.Capacity)
    } catch {
      msg.value = 'Falha ao carregar.'
    }
    try {
      const c = await api.get<{ items: Record<string, unknown>[] }>('/recharge-packages?scope=CLIENT')
      clientPkgs.value = (c.data.items ?? []).map((x) => ({
        id: x.id,
        hours: Number(x.hours),
        price: str(x.price),
      }))
    } catch {
      /* ignore */
    }
    try {
      const l = await api.get<{ items: Record<string, unknown>[] }>('/recharge-packages?scope=LOJISTA')
      lojPkgs.value = (l.data.items ?? []).map((x) => ({
        id: x.id,
        hours: Number(x.hours),
        price: str(x.price),
      }))
    } catch {
      /* ignore */
    }
  })()
})

async function save(): Promise<void> {
  msg.value = ''
  const cap = Number(capacity.value)
  const pr = Number(price.value.replace(',', '.'))
  if (cap < 1 || pr < 0.01) {
    msg.value = 'Valores inválidos.'
    return
  }
  try {
    await api.post('/settings', { pricePerHour: pr, capacity: cap })
    alert('Configurações salvas.')
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) msg.value = str((e.response?.data as { message?: string })?.message) || 'Erro.'
    else msg.value = 'Erro.'
  }
}
</script>

<style scoped></style>
