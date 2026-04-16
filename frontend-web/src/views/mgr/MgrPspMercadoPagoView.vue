<template>
  <div class="page">
    <h1>Mercado Pago (PSP)</h1>
    <p class="intro">
      Com <strong>credenciais do estacionamento</strong> desligadas, aplicam-se as variáveis globais do servidor
      (<code>MERCADOPAGO_*</code>), como antes. Com <strong>ligadas</strong>, este tenant usa apenas os valores
      guardados aqui (sandbox ou produção conforme o token).
    </p>
    <p v-if="msg" class="err">{{ msg }}</p>
    <p v-if="!canEdit" class="hint">Apenas <strong>ADMIN</strong> ou <strong>SUPER_ADMIN</strong> pode gravar alterações.</p>
    <section v-if="loading" class="hint">A carregar…</section>
    <section v-else class="form-block">
      <label class="row-check">
        <input v-model="useTenant" type="checkbox" :disabled="!canEdit" />
        <span>Usar credenciais Mercado Pago deste estacionamento (em vez do ambiente global)</span>
      </label>
      <template v-if="useTenant">
        <label>
          Ambiente
          <select v-model="environment" :disabled="!canEdit">
            <option value="SANDBOX">SANDBOX</option>
            <option value="PRODUCTION">PRODUCTION</option>
          </select>
        </label>
        <label>
          Access token
          <input
            v-model="accessToken"
            type="password"
            autocomplete="off"
            :disabled="!canEdit"
            placeholder="Obrigatório ao gravar com credenciais do tenant"
          />
        </label>
        <p v-if="loadedHasAccessToken" class="hint">Já existe token guardado; preencha de novo para o substituir.</p>
        <label>
          Segredo do webhook
          <input
            v-model="webhookSecret"
            type="password"
            autocomplete="off"
            :disabled="!canEdit"
            placeholder="Obrigatório ao gravar com credenciais do tenant"
          />
        </label>
        <p v-if="loadedHasWebhookSecret" class="hint">Já existe segredo guardado; preencha de novo para o substituir.</p>
        <label>
          Chave pública (public key)
          <input v-model="publicKey" type="text" :disabled="!canEdit" />
        </label>
        <label>
          E-mail do pagador (testes / preferência)
          <input v-model="payerEmail" type="email" :disabled="!canEdit" />
        </label>
        <label>
          URL base da API MP (opcional)
          <input v-model="apiBaseUrl" type="url" :disabled="!canEdit" placeholder="https://api.mercadopago.com" />
        </label>
        <label>
          URL de volta — sucesso (checkout hospedado, opcional)
          <input v-model="checkoutBackSuccessUrl" type="url" :disabled="!canEdit" />
        </label>
        <label>
          URL de volta — falha (opcional)
          <input v-model="checkoutBackFailureUrl" type="url" :disabled="!canEdit" />
        </label>
        <label>
          URL de volta — pendente (opcional)
          <input v-model="checkoutBackPendingUrl" type="url" :disabled="!canEdit" />
        </label>
        <label class="row-check">
          <input v-model="acknowledged" type="checkbox" :disabled="!canEdit" />
          <span
            >Confirmo que sou responsável pelas credenciais da conta Mercado Pago deste estacionamento e pela
            conformidade com as regras do PSP.</span
          >
        </label>
      </template>
      <label v-if="auth.role === 'SUPER_ADMIN' && canEdit">
        Motivo da alteração (obrigatório para SUPER_ADMIN)
        <input v-model="supportReason" type="text" maxlength="500" />
      </label>
      <section class="webhook-block">
        <h2>Webhook no Mercado Pago</h2>
        <p class="hint">
          Configure no painel do Mercado Pago um webhook <strong>POST</strong> para o URL abaixo (inclui o id do
          estacionamento).
        </p>
        <code class="webhook-url">{{ webhookDisplay }}</code>
      </section>
      <div class="actions">
        <button v-if="canEdit" type="button" class="btn-primary" :disabled="saving" @click="save">Guardar</button>
        <button type="button" @click="$router.push('/gestor/config')">Voltar às configurações</button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { getResolvedApiBase } from '@/api/http'
import { apiErrorMessage } from '@/lib/errors'
import { getJwtParkingId, parseJwtPayload } from '@/lib/jwt'
import { getActiveParkingId } from '@/session/activeParking'
import { useAuthStore } from '@/stores/auth'

type PspGet = {
  use_tenant_credentials?: boolean
  environment?: string
  public_key?: string
  payer_email?: string
  has_access_token?: boolean
  has_webhook_secret?: boolean
  api_base_url?: string | null
  checkout_back_success_url?: string | null
  checkout_back_failure_url?: string | null
  checkout_back_pending_url?: string | null
}

const api = inject<AxiosInstance>('api')!
const auth = useAuthStore()
auth.loadFromStorage()

const loading = ref(true)
const saving = ref(false)
const msg = ref('')

const useTenant = ref(false)
const environment = ref<'SANDBOX' | 'PRODUCTION'>('PRODUCTION')
const accessToken = ref('')
const webhookSecret = ref('')
const publicKey = ref('')
const payerEmail = ref('')
const apiBaseUrl = ref('')
const checkoutBackSuccessUrl = ref('')
const checkoutBackFailureUrl = ref('')
const checkoutBackPendingUrl = ref('')
const acknowledged = ref(false)
const supportReason = ref('')
const loadedHasAccessToken = ref(false)
const loadedHasWebhookSecret = ref(false)

const canEdit = computed(() => auth.role === 'ADMIN' || auth.role === 'SUPER_ADMIN')

function apiSiteOrigin(): string {
  const b = getResolvedApiBase().replace(/\/$/, '')
  return b.toLowerCase().endsWith('/api/v1') ? b.slice(0, -'/api/v1'.length) : b
}

const parkingIdForWebhook = computed(() => {
  const active = getActiveParkingId()
  if (active) return active
  const t = auth.accessToken
  if (!t) return ''
  try {
    return getJwtParkingId(parseJwtPayload(t)) ?? ''
  } catch {
    return ''
  }
})

const webhookDisplay = computed(() => {
  const pid = parkingIdForWebhook.value
  if (!pid) {
    return '(defina o estacionamento ativo no super admin, ou inicie sessão com um utilizador do tenant para ver o URL)'
  }
  return `${apiSiteOrigin()}/api/v1/payments/webhook/psp/mercadopago/${pid}`
})

onMounted(() => {
  void (async () => {
    msg.value = ''
    loading.value = true
    try {
      const { data } = await api.get<PspGet>('/settings/psp/mercadopago')
      useTenant.value = Boolean(data.use_tenant_credentials)
      const env = (data.environment ?? 'PRODUCTION').toUpperCase()
      environment.value = env === 'SANDBOX' ? 'SANDBOX' : 'PRODUCTION'
      publicKey.value = data.public_key ?? ''
      payerEmail.value = data.payer_email ?? ''
      apiBaseUrl.value = data.api_base_url ?? ''
      checkoutBackSuccessUrl.value = data.checkout_back_success_url ?? ''
      checkoutBackFailureUrl.value = data.checkout_back_failure_url ?? ''
      checkoutBackPendingUrl.value = data.checkout_back_pending_url ?? ''
      loadedHasAccessToken.value = Boolean(data.has_access_token)
      loadedHasWebhookSecret.value = Boolean(data.has_webhook_secret)
      accessToken.value = ''
      webhookSecret.value = ''
      acknowledged.value = false
      supportReason.value = ''
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) msg.value = apiErrorMessage(e.response?.data) || 'Falha ao carregar.'
      else msg.value = 'Falha ao carregar.'
    } finally {
      loading.value = false
    }
  })()
})

async function save(): Promise<void> {
  if (!canEdit.value) return
  msg.value = ''
  if (auth.role === 'SUPER_ADMIN' && !supportReason.value.trim()) {
    msg.value = 'SUPER_ADMIN deve indicar o motivo da alteração.'
    return
  }
  if (useTenant.value) {
    if (!acknowledged.value) {
      msg.value = 'Marque a confirmação de responsabilidade para gravar credenciais do tenant.'
      return
    }
    if (
      !accessToken.value.trim() ||
      !webhookSecret.value.trim() ||
      !publicKey.value.trim() ||
      !payerEmail.value.trim()
    ) {
      msg.value = 'Com credenciais do tenant: access token, segredo do webhook, chave pública e e-mail são obrigatórios.'
      return
    }
  }

  saving.value = true
  try {
    await api.put('/settings/psp/mercadopago', {
      useTenantCredentials: useTenant.value,
      acknowledged: useTenant.value ? acknowledged.value : false,
      environment: useTenant.value ? environment.value : null,
      accessToken: useTenant.value ? accessToken.value.trim() : null,
      webhookSecret: useTenant.value ? webhookSecret.value.trim() : null,
      publicKey: useTenant.value ? publicKey.value.trim() : null,
      payerEmail: useTenant.value ? payerEmail.value.trim() : null,
      apiBaseUrl: useTenant.value && apiBaseUrl.value.trim() ? apiBaseUrl.value.trim() : null,
      checkoutBackSuccessUrl:
        useTenant.value && checkoutBackSuccessUrl.value.trim() ? checkoutBackSuccessUrl.value.trim() : null,
      checkoutBackFailureUrl:
        useTenant.value && checkoutBackFailureUrl.value.trim() ? checkoutBackFailureUrl.value.trim() : null,
      checkoutBackPendingUrl:
        useTenant.value && checkoutBackPendingUrl.value.trim() ? checkoutBackPendingUrl.value.trim() : null,
      supportReason: auth.role === 'SUPER_ADMIN' ? supportReason.value.trim() : null,
    })
    alert('Configuração PSP guardada.')
    accessToken.value = ''
    webhookSecret.value = ''
    acknowledged.value = false
    supportReason.value = ''
    const { data } = await api.get<PspGet>('/settings/psp/mercadopago')
    loadedHasAccessToken.value = Boolean(data.has_access_token)
    loadedHasWebhookSecret.value = Boolean(data.has_webhook_secret)
    useTenant.value = Boolean(data.use_tenant_credentials)
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) msg.value = apiErrorMessage(e.response?.data) || 'Erro ao guardar.'
    else msg.value = 'Erro ao guardar.'
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.intro {
  max-width: 48rem;
  color: #444;
  line-height: 1.45;
}

.form-block {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-width: 42rem;
}

.form-block label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.9rem;
}

.form-block input:not([type='checkbox']),
.form-block select {
  padding: 0.45rem 0.5rem;
  border: 1px solid #ccc;
  border-radius: 4px;
}

.row-check {
  flex-direction: row !important;
  align-items: flex-start;
  gap: 0.5rem !important;
}

.row-check input {
  margin-top: 0.2rem;
}

.hint {
  margin: 0;
  font-size: 0.85rem;
  color: #555;
}

.err {
  color: #c62828;
}

.webhook-block {
  margin-top: 1rem;
  padding: 0.85rem;
  border: 1px solid #ddd;
  border-radius: 6px;
}

.webhook-block h2 {
  margin: 0 0 0.5rem;
  font-size: 1rem;
}

.webhook-url {
  display: block;
  margin-top: 0.5rem;
  padding: 0.5rem;
  background: #f5f5f5;
  word-break: break-all;
  font-size: 0.8rem;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-top: 1rem;
}
</style>
