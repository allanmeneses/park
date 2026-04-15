<template>
  <div class="page">
    <h1>Nova entrada</h1>
    <div class="field">
      <label for="plate">Placa do veículo</label>
      <input
        id="plate"
        :value="plateDisplay"
        type="text"
        inputmode="text"
        enterkeyhint="done"
        maxlength="8"
        autocapitalize="characters"
        autocomplete="off"
        spellcheck="false"
        aria-label="Placa do veículo"
        placeholder="ABC-1D23"
        autofocus
        @input="onPlateInput"
        @blur="onPlateBlur"
        @keydown.enter.prevent="send"
      />
      <p v-if="fieldErr" class="err">{{ fieldErr }}</p>
    </div>
    <p v-if="msg" class="err">{{ msg }}</p>
    <button type="button" class="btn-primary" aria-label="Confirmar" @click="send">Confirmar</button>
    <button type="button" style="margin-left: 0.5rem" aria-label="Voltar" @click="$router.back()">Voltar</button>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, nextTick, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import axios from 'axios'
import {
  formatPlateDisplay,
  isValidPlate,
  plateDisplayIndexToRawLength,
  plateRawLengthToDisplayIndex,
  sanitizePlateInput,
} from '@/lib/plate'
import { apiErrorMessage } from '@/lib/errors'
import { useOfflineQueueStore } from '@/stores/offlineQueue'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const queue = useOfflineQueueStore()

/** Valor sem hífen (até 7 caracteres), enviado à API. */
const plateRaw = ref('')
const plateDisplay = computed(() => formatPlateDisplay(plateRaw.value))
const fieldErr = ref('')
const msg = ref('')

function onPlateInput(e: Event): void {
  const el = e.target as HTMLInputElement
  const start = el.selectionStart ?? 0
  const end = el.selectionEnd ?? 0
  const beforeDisp = formatPlateDisplay(plateRaw.value)
  const rawCursor = plateDisplayIndexToRawLength(start, el.value)
  plateRaw.value = sanitizePlateInput(el.value)
  const afterRaw = plateRaw.value
  nextTick(() => {
    let pos: number
    if (start === end && start >= beforeDisp.length) {
      pos = formatPlateDisplay(afterRaw).length
    } else {
      const clampedRaw = Math.min(rawCursor, afterRaw.length)
      pos = plateRawLengthToDisplayIndex(clampedRaw)
    }
    el.setSelectionRange(pos, pos)
  })
}

function onPlateBlur(): void {
  plateRaw.value = sanitizePlateInput(plateRaw.value)
}

async function send(): Promise<void> {
  fieldErr.value = ''
  msg.value = ''
  const p = sanitizePlateInput(plateRaw.value)
  plateRaw.value = p
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
