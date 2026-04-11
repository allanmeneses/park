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
    <section class="pkg-section">
      <h2>Pacotes — Cliente</h2>
      <p v-if="clientPkgMsg" class="err">{{ clientPkgMsg }}</p>
      <div v-if="canManagePackages" class="pkg-form">
        <h3>{{ clientDraft.id ? 'Editar pacote' : 'Novo pacote' }}</h3>
        <div class="pkg-grid">
          <label>
            Nome
            <input v-model="clientDraft.display_name" type="text" maxlength="120" />
          </label>
          <label>
            Horas
            <input v-model="clientDraft.hours" type="number" min="1" />
          </label>
          <label>
            Preço
            <input v-model="clientDraft.price" type="text" inputmode="decimal" />
          </label>
          <label>
            Ordem
            <input v-model="clientDraft.sort_order" type="number" min="0" />
          </label>
        </div>
        <label class="pkg-check">
          <input v-model="clientDraft.is_promo" type="checkbox" />
          <span>Promocional</span>
        </label>
        <label class="pkg-check">
          <input v-model="clientDraft.active" type="checkbox" />
          <span>Ativo</span>
        </label>
        <div class="pkg-actions">
          <button type="button" class="btn-primary" :disabled="savingScope === 'CLIENT'" @click="savePackage('CLIENT')">
            {{ clientDraft.id ? 'Salvar pacote' : 'Criar pacote' }}
          </button>
          <button v-if="clientDraft.id" type="button" :disabled="savingScope === 'CLIENT'" @click="resetDraft('CLIENT')">
            Cancelar edição
          </button>
        </div>
      </div>
      <p v-if="!clientPkgs.length">Nenhum pacote cadastrado para este tipo.</p>
      <ul v-else class="pkg-list">
        <li v-for="p in clientPkgs" :key="p.id" class="pkg-item">
          <div class="pkg-line">
            <strong>{{ p.display_name || `${p.hours} h` }}</strong>
            <span v-if="p.is_promo" class="pkg-badge promo">Promocional</span>
            <span v-if="!p.active" class="pkg-badge inactive">Inativo</span>
          </div>
          <p class="pkg-meta">{{ p.hours }} h — R$ {{ p.price }}</p>
          <p class="pkg-meta">Ordem: {{ p.sort_order }}</p>
          <div v-if="canManagePackages" class="pkg-actions">
            <button type="button" @click="editPackage('CLIENT', p)">Editar</button>
            <button type="button" @click="toggleActive('CLIENT', p, !p.active)">
              {{ p.active ? 'Desativar' : 'Reativar' }}
            </button>
            <button type="button" :disabled="savingScope === 'CLIENT'" @click="deletePackage('CLIENT', p)">
              Excluir
            </button>
          </div>
        </li>
      </ul>
    </section>
    <section class="pkg-section">
      <h2>Pacotes — Lojista</h2>
      <p v-if="lojPkgMsg" class="err">{{ lojPkgMsg }}</p>
      <div v-if="canManagePackages" class="pkg-form">
        <h3>{{ lojDraft.id ? 'Editar pacote' : 'Novo pacote' }}</h3>
        <div class="pkg-grid">
          <label>
            Nome
            <input v-model="lojDraft.display_name" type="text" maxlength="120" />
          </label>
          <label>
            Horas
            <input v-model="lojDraft.hours" type="number" min="1" />
          </label>
          <label>
            Preço
            <input v-model="lojDraft.price" type="text" inputmode="decimal" />
          </label>
          <label>
            Ordem
            <input v-model="lojDraft.sort_order" type="number" min="0" />
          </label>
        </div>
        <label class="pkg-check">
          <input v-model="lojDraft.is_promo" type="checkbox" />
          <span>Promocional</span>
        </label>
        <label class="pkg-check">
          <input v-model="lojDraft.active" type="checkbox" />
          <span>Ativo</span>
        </label>
        <div class="pkg-actions">
          <button type="button" class="btn-primary" :disabled="savingScope === 'LOJISTA'" @click="savePackage('LOJISTA')">
            {{ lojDraft.id ? 'Salvar pacote' : 'Criar pacote' }}
          </button>
          <button v-if="lojDraft.id" type="button" :disabled="savingScope === 'LOJISTA'" @click="resetDraft('LOJISTA')">
            Cancelar edição
          </button>
        </div>
      </div>
      <p v-if="!lojPkgs.length">Nenhum pacote cadastrado para este tipo.</p>
      <ul v-else class="pkg-list">
        <li v-for="p in lojPkgs" :key="p.id" class="pkg-item">
          <div class="pkg-line">
            <strong>{{ p.display_name || `${p.hours} h` }}</strong>
            <span v-if="p.is_promo" class="pkg-badge promo">Promocional</span>
            <span v-if="!p.active" class="pkg-badge inactive">Inativo</span>
          </div>
          <p class="pkg-meta">{{ p.hours }} h — R$ {{ p.price }}</p>
          <p class="pkg-meta">Ordem: {{ p.sort_order }}</p>
          <div v-if="canManagePackages" class="pkg-actions">
            <button type="button" @click="editPackage('LOJISTA', p)">Editar</button>
            <button type="button" @click="toggleActive('LOJISTA', p, !p.active)">
              {{ p.active ? 'Desativar' : 'Reativar' }}
            </button>
            <button type="button" :disabled="savingScope === 'LOJISTA'" @click="deletePackage('LOJISTA', p)">
              Excluir
            </button>
          </div>
        </li>
      </ul>
    </section>
    <button type="button" style="margin-top: 1rem" aria-label="Voltar" @click="$router.push('/gestor')">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import {
  compareRechargePackages,
  rechargePackageFromApi,
  str,
  type RechargePackageDto,
} from '@/lib/apiDto'
import { useAuthStore } from '@/stores/auth'
import MgrLojistaInvitesSection from '@/components/MgrLojistaInvitesSection.vue'

type PackageScope = 'CLIENT' | 'LOJISTA'
type PackageForm = {
  id: string
  display_name: string
  hours: string
  price: string
  is_promo: boolean
  sort_order: string
  active: boolean
}

const api = inject<AxiosInstance>('api')!
const auth = useAuthStore()
auth.loadFromStorage()
const price = ref('')
const capacity = ref('1')
const msg = ref('')
const clientPkgs = ref<RechargePackageDto[]>([])
const lojPkgs = ref<RechargePackageDto[]>([])
const clientPkgMsg = ref('')
const lojPkgMsg = ref('')
const savingScope = ref<PackageScope | ''>('')
const clientDraft = ref<PackageForm>(createPackageForm())
const lojDraft = ref<PackageForm>(createPackageForm())

const showLojistaInvites = computed(
  () => auth.role === 'ADMIN' || auth.role === 'SUPER_ADMIN',
)
const canManagePackages = computed(
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
    await Promise.all([loadPackages('CLIENT'), loadPackages('LOJISTA')])
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

function createPackageForm(): PackageForm {
  return {
    id: '',
    display_name: '',
    hours: '',
    price: '',
    is_promo: false,
    sort_order: '0',
    active: true,
  }
}

function packageListRef(scope: PackageScope) {
  return scope === 'CLIENT' ? clientPkgs : lojPkgs
}

function packageMsgRef(scope: PackageScope) {
  return scope === 'CLIENT' ? clientPkgMsg : lojPkgMsg
}

function packageDraftRef(scope: PackageScope) {
  return scope === 'CLIENT' ? clientDraft : lojDraft
}

async function loadPackages(scope: PackageScope): Promise<void> {
  const pkgMsg = packageMsgRef(scope)
  try {
    const path = canManagePackages.value
      ? `/recharge-packages/manage?scope=${scope}`
      : `/recharge-packages?scope=${scope}`
    const { data } = await api.get<{ items: Record<string, unknown>[] }>(path)
    packageListRef(scope).value = (data.items ?? [])
      .map(rechargePackageFromApi)
      .sort(compareRechargePackages)
    pkgMsg.value = ''
  } catch (e: unknown) {
    packageListRef(scope).value = []
    if (axios.isAxiosError(e)) pkgMsg.value = apiErrorMessage(e.response?.data)
    else pkgMsg.value = 'Falha ao carregar pacotes.'
  }
}

function resetDraft(scope: PackageScope): void {
  packageDraftRef(scope).value = createPackageForm()
  packageMsgRef(scope).value = ''
}

function editPackage(scope: PackageScope, pkg: RechargePackageDto): void {
  packageDraftRef(scope).value = {
    id: pkg.id,
    display_name: pkg.display_name,
    hours: String(pkg.hours),
    price: pkg.price,
    is_promo: pkg.is_promo,
    sort_order: String(pkg.sort_order),
    active: pkg.active,
  }
  packageMsgRef(scope).value = ''
}

function buildPackagePayload(scope: PackageScope): {
  displayName: string
  scope: PackageScope
  hours: number
  price: number
  isPromo: boolean
  sortOrder: number
  active: boolean
} | null {
  const draft = packageDraftRef(scope).value
  const name = draft.display_name.trim()
  const hours = Number(draft.hours)
  const price = Number(draft.price.replace(',', '.'))
  const sortOrder = Number(draft.sort_order)

  if (!name) {
    packageMsgRef(scope).value = 'Nome do pacote é obrigatório.'
    return null
  }
  if (!Number.isFinite(hours) || hours < 1) {
    packageMsgRef(scope).value = 'Horas inválidas.'
    return null
  }
  if (!Number.isFinite(price) || price < 0) {
    packageMsgRef(scope).value = 'Preço inválido.'
    return null
  }
  if (!Number.isFinite(sortOrder) || sortOrder < 0) {
    packageMsgRef(scope).value = 'Ordem inválida.'
    return null
  }

  return {
    displayName: name,
    scope,
    hours,
    price,
    isPromo: draft.is_promo,
    sortOrder,
    active: draft.active,
  }
}

async function savePackage(scope: PackageScope): Promise<void> {
  if (!canManagePackages.value) return
  const draft = packageDraftRef(scope).value
  const payload = buildPackagePayload(scope)
  if (!payload) return

  savingScope.value = scope
  packageMsgRef(scope).value = ''
  try {
    if (draft.id) await api.put(`/recharge-packages/${draft.id}`, payload)
    else await api.post('/recharge-packages', payload)
    await loadPackages(scope)
    resetDraft(scope)
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) packageMsgRef(scope).value = apiErrorMessage(e.response?.data)
    else packageMsgRef(scope).value = 'Não foi possível salvar o pacote.'
  } finally {
    savingScope.value = ''
  }
}

async function toggleActive(scope: PackageScope, pkg: RechargePackageDto, active: boolean): Promise<void> {
  if (!canManagePackages.value) return
  savingScope.value = scope
  packageMsgRef(scope).value = ''
  try {
    await api.put(`/recharge-packages/${pkg.id}`, {
      displayName: pkg.display_name,
      scope,
      hours: pkg.hours,
      price: Number(pkg.price.replace(',', '.')),
      isPromo: pkg.is_promo,
      sortOrder: pkg.sort_order,
      active,
    })
    await loadPackages(scope)
    if (packageDraftRef(scope).value.id === pkg.id) resetDraft(scope)
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) packageMsgRef(scope).value = apiErrorMessage(e.response?.data)
    else packageMsgRef(scope).value = 'Não foi possível atualizar o pacote.'
  } finally {
    savingScope.value = ''
  }
}

async function deletePackage(scope: PackageScope, pkg: RechargePackageDto): Promise<void> {
  if (!canManagePackages.value) return
  if (!confirm(`Excluir permanentemente o pacote "${pkg.display_name || `${pkg.hours} h`}"?`)) return

  savingScope.value = scope
  packageMsgRef(scope).value = ''
  try {
    await api.delete(`/recharge-packages/${pkg.id}`)
    await loadPackages(scope)
    if (packageDraftRef(scope).value.id === pkg.id) resetDraft(scope)
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) packageMsgRef(scope).value = apiErrorMessage(e.response?.data)
    else packageMsgRef(scope).value = 'Não foi possível excluir o pacote.'
  } finally {
    savingScope.value = ''
  }
}
</script>

<style scoped>
.pkg-section {
  margin-top: 2rem;
}

.pkg-section h2 {
  font-size: 1rem;
}

.pkg-form {
  margin: 0.75rem 0 1rem;
  padding: 0.85rem;
  border: 1px solid #ddd;
  border-radius: 0.5rem;
  max-width: 42rem;
}

.pkg-form h3 {
  margin-top: 0;
  font-size: 0.95rem;
}

.pkg-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(10rem, 1fr));
  gap: 0.75rem;
}

.pkg-grid label,
.pkg-check {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.pkg-check {
  flex-direction: row;
  align-items: center;
  margin-top: 0.65rem;
}

.pkg-list {
  list-style: none;
  padding: 0;
  max-width: 42rem;
}

.pkg-item {
  margin-bottom: 0.75rem;
  padding: 0.75rem;
  border: 1px solid #e3e3e3;
  border-radius: 0.5rem;
}

.pkg-line {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.pkg-meta {
  margin: 0.25rem 0 0;
  color: #555;
}

.pkg-actions {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.65rem;
  flex-wrap: wrap;
}

.pkg-badge {
  display: inline-block;
  padding: 0.1rem 0.45rem;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 600;
}

.pkg-badge.promo {
  background: #fff3cd;
  color: #7a5600;
}

.pkg-badge.inactive {
  background: #efefef;
  color: #555;
}
</style>
