<template>
  <div class="page">
    <h1>Super administrador</h1>
    <p class="subtle">
      Só o <strong>super administrador</strong> cria novos estacionamentos. O <strong>administrador</strong> (ADMIN) acede apenas ao estacionamento do seu
      login e não vê este ecrã.
    </p>

    <h2>Novo estacionamento</h2>
    <p class="subtle">Indique o e-mail e a senha do <strong>administrador do tenant</strong> e do <strong>primeiro operador</strong> (contas distintas).</p>
    <form class="stack" @submit.prevent="createTenant">
      <div class="field">
        <label for="adm-mail">E-mail do administrador</label>
        <input id="adm-mail" v-model="cAdminEmail" type="email" autocomplete="off" />
      </div>
      <div class="field">
        <label for="adm-pass">Senha do administrador</label>
        <input id="adm-pass" v-model="cAdminPass" type="password" autocomplete="new-password" />
      </div>
      <div class="field">
        <label for="op-mail">E-mail do operador</label>
        <input id="op-mail" v-model="cOpEmail" type="email" autocomplete="off" />
      </div>
      <div class="field">
        <label for="op-pass">Senha do operador</label>
        <input id="op-pass" v-model="cOpPass" type="password" autocomplete="new-password" />
      </div>
      <p v-if="createErr" class="err">{{ createErr }}</p>
      <p v-if="createOk" class="ok">{{ createOk }}</p>
      <button type="submit" class="btn-primary" :disabled="createLoading" aria-label="Criar estacionamento">
        {{ createLoading ? 'A criar…' : 'Criar estacionamento' }}
      </button>
    </form>

    <h2 style="margin-top: 2rem">Estacionamento ativo</h2>
    <div class="field">
      <label for="tenant-select">Lista (nome / identificador)</label>
      <select id="tenant-select" v-model="selectedParkingId" @change="onSelect">
        <option value="" disabled hidden>— Selecione —</option>
        <option v-for="t in tenants" :key="t.parkingId" :value="t.parkingId">
          {{ t.label || t.parkingId }}
        </option>
      </select>
    </div>
    <p v-if="loading">A carregar lista...</p>
    <p v-if="listErr" class="err">{{ listErr }}</p>

    <details class="tech">
      <summary>Identificador técnico (UUID)</summary>
      <div class="field">
        <label for="pid">ID do estacionamento (UUID)</label>
        <input id="pid" v-model="parkingId" type="text" autocomplete="off" />
      </div>
      <button type="button" class="btn-primary" aria-label="Definir" @click="setId">Definir</button>
    </details>
    <p v-if="hint" class="err">{{ hint }}</p>
    <div style="margin-top: 2rem; display: flex; gap: 0.5rem; flex-wrap: wrap">
      <button type="button" class="btn-primary" :aria-label="STRINGS.B20" @click="goMgr">
        {{ STRINGS.B20 }}
      </button>
      <button type="button" class="btn-primary" :aria-label="STRINGS.B21" @click="goOp">
        {{ STRINGS.B21 }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'
import type { AxiosInstance } from 'axios'
import { getActiveParkingId, setActiveParkingId } from '@/session/activeParking'
import { isValidParkingUuid } from '@/lib/uuidParking'
import { STRINGS } from '@/strings'
import { apiErrorMessage } from '@/lib/errors'

const router = useRouter()
const api = inject<AxiosInstance>('api')
const parkingId = ref('')
const selectedParkingId = ref('')
const hint = ref('')
const loading = ref(false)
const listErr = ref('')
const tenants = ref<Array<{ parkingId: string; label: string }>>([])

const cAdminEmail = ref('')
const cAdminPass = ref('')
const cOpEmail = ref('')
const cOpPass = ref('')
const createLoading = ref(false)
const createErr = ref('')
const createOk = ref('')

function onSelect(): void {
  const s = selectedParkingId.value.trim()
  if (!isValidParkingUuid(s)) return
  parkingId.value = s.toLowerCase()
  setActiveParkingId(parkingId.value)
}

function setId(): void {
  hint.value = ''
  const s = parkingId.value.trim()
  if (!isValidParkingUuid(s)) {
    hint.value = 'UUID inválido.'
    return
  }
  setActiveParkingId(s.toLowerCase())
  selectedParkingId.value = s.toLowerCase()
}

function goMgr(): void {
  if (!check()) return
  router.push('/gestor')
}

function goOp(): void {
  if (!check()) return
  router.push('/operador')
}

function check(): boolean {
  const s = parkingId.value.trim()
  if (!isValidParkingUuid(s)) {
    hint.value = STRINGS.S15
    return false
  }
  setActiveParkingId(s.toLowerCase())
  return true
}

async function createTenant(): Promise<void> {
  createErr.value = ''
  createOk.value = ''
  if (!api) {
    createErr.value = 'API indisponível.'
    return
  }
  const ae = cAdminEmail.value.trim()
  const oe = cOpEmail.value.trim()
  if (!ae || !cAdminPass.value || !oe || !cOpPass.value) {
    createErr.value = STRINGS.E3
    return
  }
  if (ae.toLowerCase() === oe.toLowerCase()) {
    createErr.value = 'O administrador e o operador devem ter e-mails diferentes.'
    return
  }
  createLoading.value = true
  try {
    await api.post('/admin/tenants', {
      adminEmail: ae,
      adminPassword: cAdminPass.value,
      operatorEmail: oe,
      operatorPassword: cOpPass.value,
    })
    createOk.value = 'Estacionamento criado. Pode selecioná-lo na lista.'
    cAdminEmail.value = ''
    cAdminPass.value = ''
    cOpEmail.value = ''
    cOpPass.value = ''
    await loadTenants()
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) createErr.value = apiErrorMessage(e.response?.data, 'Falha ao criar.')
    else createErr.value = 'Falha ao criar.'
  } finally {
    createLoading.value = false
  }
}

async function loadTenants(): Promise<void> {
  if (!api) {
    listErr.value = 'API indisponível.'
    return
  }
  loading.value = true
  listErr.value = ''
  try {
    const { data } = await api.get('/admin/tenants')
    const rawItems = Array.isArray(data?.items) ? data.items : []
    tenants.value = rawItems
      .filter((x: unknown) => typeof x === 'object' && x !== null)
      .map((x: { parkingId?: unknown; label?: unknown; parking_id?: unknown }) => {
        const pid = typeof x.parkingId === 'string' ? x.parkingId : typeof x.parking_id === 'string' ? x.parking_id : ''
        const label = typeof x.label === 'string' ? x.label : ''
        return { parkingId: pid.toLowerCase(), label }
      })
      .filter((x: { parkingId: string }) => isValidParkingUuid(x.parkingId))
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) listErr.value = apiErrorMessage(e.response?.data, 'Não foi possível carregar a lista de estacionamentos.')
    else listErr.value = 'Não foi possível carregar a lista de estacionamentos.'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  const cur = getActiveParkingId()
  if (cur && isValidParkingUuid(cur)) {
    parkingId.value = cur
    selectedParkingId.value = cur
  }
  void loadTenants()
})
</script>

<style scoped>
.subtle {
  color: #555;
  font-size: 0.95rem;
  max-width: 40rem;
}
.stack {
  max-width: 24rem;
}
.ok {
  color: #2e7d32;
}
</style>
