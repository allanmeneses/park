<template>
  <div class="page">
    <h1>Bonificar cliente</h1>
    <p class="sub">Conceda horas bonificadas por placa. O saldo bonificado é controlado separado da carteira comprada do cliente.</p>
    <p v-if="restrictHint" class="warn">{{ restrictHint }}</p>
    <div class="field">
      <label for="plate">Placa do veículo</label>
      <input id="plate" v-model="plate" type="text" autocomplete="off" maxlength="10" aria-label="Placa" />
    </div>
    <div class="field">
      <label for="hours">Horas a bonificar</label>
      <input id="hours" v-model.number="hours" type="number" min="1" max="720" aria-label="Horas" />
    </div>
    <p v-if="fieldErr" class="err">{{ fieldErr }}</p>
    <p v-if="msg" class="err">{{ msg }}</p>
    <p v-if="okMsg" class="ok">{{ okMsg }}</p>
    <button type="button" class="btn-primary" :disabled="loading" aria-label="Confirmar bonificação" @click="submit">
      Confirmar
    </button>
    <button type="button" class="btn-secondary" style="margin-left: 0.5rem; margin-top: 0.5rem" aria-label="Voltar" @click="$router.push('/lojista')">
      Voltar
    </button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, ref } from 'vue'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { grantClientBalanceHours, str } from '@/lib/apiDto'
import { apiErrorMessage } from '@/lib/errors'
import { isValidPlate, normalizePlate } from '@/lib/plate'
import { STRINGS } from '@/strings'

const api = inject<AxiosInstance>('api')!

const plate = ref('')
const hours = ref(1)
const restrictHint = ref('')
const fieldErr = ref('')
const msg = ref('')
const okMsg = ref('')
const loading = ref(false)

onMounted(() => {
  void (async () => {
    try {
      const { data } = await api.get<{ allow_grant_before_entry?: boolean }>('/lojista/grant-settings')
      if (data.allow_grant_before_entry === false) restrictHint.value = STRINGS.S19
    } catch {
      /* silencioso: fluxo de bonificar continua */
    }
  })()
})

async function submit(): Promise<void> {
  fieldErr.value = ''
  msg.value = ''
  okMsg.value = ''
  const p = normalizePlate(plate.value)
  plate.value = p
  if (!isValidPlate(p)) {
    fieldErr.value = 'Placa inválida.'
    return
  }
  const h = Number(hours.value)
  if (!Number.isFinite(h) || h < 1 || h > 720) {
    fieldErr.value = 'Informe entre 1 e 720 horas.'
    return
  }
  loading.value = true
  try {
    const { data } = await api.post(
      '/lojista/grant-client',
      { plate: p, hours: h },
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    )
    const raw = data as Record<string, unknown>
    const ch = grantClientBalanceHours(raw)
    const lh = raw.lojista_balance_hours ?? raw.lojistaBalanceHours
    const chDisp = ch == null ? '—' : String(ch)
    okMsg.value = [
      `Registámos ${h} h de bonificação para ${p}.`,
      `Saldo bonificado disponível para esta placa (horas concedidas menos as já aplicadas em checkout prévio ou em pré-fatura): ${chDisp} h.`,
      `Saldo da sua carteira lojista: ${str(lh)} h.`,
      h > 0 && ch === 0
        ? 'Se o cliente ainda só vai ao caixa sair, mas aqui aparece 0 h, há consumo contabilizado noutra estadia desta placa ou um checkout já iniciado (valor pendente).'
        : '',
    ]
      .filter(Boolean)
      .join(' ')
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) msg.value = apiErrorMessage(e.response?.data)
    else msg.value = 'Falha ao bonificar.'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.sub {
  color: #616161;
  max-width: 28rem;
}
.field {
  margin: 0.75rem 0;
}
.err {
  color: #c62828;
}
.ok {
  color: #2e7d32;
}
.warn {
  color: #e65100;
  max-width: 28rem;
  font-size: 0.95rem;
}
</style>
