<template>
  <div class="page">
    <h1>Carteira de convênio</h1>
    <p v-if="err" class="err">{{ err }}</p>
    <p v-else>Saldo: {{ bal }} horas</p>

    <section v-if="!grantErr" class="grant-pref" aria-labelledby="grant-pref-title">
      <h2 id="grant-pref-title" class="subtle">Bonificação a clientes</h2>
      <label class="switch-row">
        <input
          v-model="restrictToLot"
          type="checkbox"
          role="switch"
          :disabled="grantLoading"
          :aria-checked="restrictToLot"
          :aria-label="strings.B30"
          @change="onGrantPrefChange"
        />
        <span class="switch-label">{{ strings.B30 }}</span>
      </label>
      <p class="hint">{{ restrictToLot ? strings.S18 : strings.S17 }}</p>
      <p v-if="grantSaveErr" class="err">{{ grantSaveErr }}</p>
    </section>
    <p v-else class="err">{{ grantErr }}</p>

    <button type="button" class="btn-primary" style="margin-top: 1rem" aria-label="Comprar horas" @click="$router.push('/lojista/comprar')">
      Comprar horas
    </button>
    <button type="button" class="btn-primary" aria-label="Histórico" style="margin-left: 0.5rem" @click="$router.push('/lojista/historico')">
      Histórico
    </button>
    <button type="button" class="btn-primary" aria-label="Bonificar cliente" style="margin-top: 0.5rem; margin-right: 0.5rem" @click="$router.push('/lojista/bonificar')">
      Bonificar cliente
    </button>
    <button type="button" class="btn-primary" aria-label="Extrato de bonificações" style="margin-top: 0.5rem" @click="$router.push('/lojista/bonificacoes')">
      Extrato de bonificações
    </button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { apiErrorMessage } from '@/lib/errors'
import { STRINGS } from '@/strings'

const strings = STRINGS

const api = inject<AxiosInstance>('api')!
const bal = ref(0)
const err = ref('')
const grantLoading = ref(true)
const grantErr = ref('')
const grantSaveErr = ref('')
/** true = só no pátio (= allow_grant_before_entry false) */
const restrictToLot = ref(false)
let prefHydrating = true

onMounted(() => {
  void (async () => {
    grantLoading.value = true
    try {
      const { data: w } = await api.get<{ balance_hours?: number }>('/lojista/wallet')
      bal.value = w.balance_hours ?? 0
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) err.value = apiErrorMessage(e.response?.data)
      else err.value = 'Erro.'
    }

    try {
      const { data: g } = await api.get<{ allow_grant_before_entry?: boolean }>('/lojista/grant-settings')
      const allowBefore = g.allow_grant_before_entry !== false
      restrictToLot.value = !allowBefore
    } catch (e: unknown) {
      if (axios.isAxiosError(e)) grantErr.value = apiErrorMessage(e.response?.data)
      else grantErr.value = 'Não foi possível carregar preferências de bonificação.'
    } finally {
      grantLoading.value = false
      prefHydrating = false
    }
  })()
})

async function onGrantPrefChange(): Promise<void> {
  if (prefHydrating) return
  grantSaveErr.value = ''
  const allowBefore = !restrictToLot.value
  grantLoading.value = true
  try {
    await api.put('/lojista/grant-settings', { allow_grant_before_entry: allowBefore })
  } catch (e: unknown) {
    restrictToLot.value = !restrictToLot.value
    if (axios.isAxiosError(e)) grantSaveErr.value = apiErrorMessage(e.response?.data)
    else grantSaveErr.value = 'Não foi possível salvar.'
  } finally {
    grantLoading.value = false
  }
}
</script>

<style scoped>
.grant-pref {
  margin-top: 1.25rem;
  padding: 0.75rem 0;
  border-top: 1px solid #e0e0e0;
  max-width: 28rem;
}
.subtle {
  font-size: 0.95rem;
  font-weight: 600;
  color: #424242;
  margin: 0 0 0.5rem;
}
.switch-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}
.switch-label {
  color: #212121;
}
.hint {
  margin: 0.5rem 0 0;
  font-size: 0.9rem;
  color: #616161;
}
.err {
  color: #c62828;
}
</style>
