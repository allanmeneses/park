<template>
  <div class="page">
    <h1>Nova entrada</h1>
    <div class="field">
      <label for="plate">Placa do veículo</label>
      <input
        id="plate"
        v-model="plate"
        type="text"
        maxlength="10"
        aria-label="Placa do veículo"
        @blur="plate = normalizePlate(plate)"
      />
      <p v-if="fieldErr" class="err">{{ fieldErr }}</p>
    </div>
    <p v-if="msg" class="err">{{ msg }}</p>
    <button type="button" class="btn-primary" aria-label="Confirmar" @click="send">Confirmar</button>
    <button type="button" style="margin-left: 0.5rem" aria-label="Voltar" @click="$router.back()">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { inject, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import { isValidPlate, normalizePlate } from '@/lib/plate'
import { apiErrorMessage } from '@/lib/errors'
import { useOfflineQueueStore } from '@/stores/offlineQueue'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const queue = useOfflineQueueStore()

const plate = ref('')
const fieldErr = ref('')
const msg = ref('')

async function send(): Promise<void> {
  fieldErr.value = ''
  msg.value = ''
  const p = normalizePlate(plate.value)
  plate.value = p
  if (!isValidPlate(p)) {
    fieldErr.value = 'Formato de placa inválido.'
    return
  }
  const idem = crypto.randomUUID()
  if (typeof navigator !== 'undefined' && !navigator.onLine) {
    const token = sessionStorage.getItem('parking.v1.access') ?? ''
    queue.enqueue({
      id_local: crypto.randomUUID(),
      method: 'POST',
      path: '/tickets',
      headers: {
        'Idempotency-Key': idem,
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: { plate: p },
      created_at_epoch: Math.floor(Date.now() / 1000),
    })
    msg.value = 'Sem conexão: operação enfileirada.'
    return
  }
  try {
    await api.post(
      '/tickets',
      { plate: p },
      { headers: { 'Idempotency-Key': idem } },
    )
    await router.replace('/operador')
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) {
      const code = (e.response?.data as { code?: string } | undefined)?.code
      if (code === 'PLATE_INVALID') {
        fieldErr.value = 'Formato de placa inválido.'
        return
      }
      if (code === 'PLATE_HAS_ACTIVE_TICKET') {
        msg.value = 'Já existe ticket em aberto para esta placa.'
        return
      }
      msg.value = apiErrorMessage(e.response?.data)
      return
    }
    msg.value = 'Erro.'
  }
}
</script>
