<template>
  <div class="page">
    <h1>Super — Tenant</h1>
    <div class="field">
      <label for="pid">ID do estacionamento (UUID)</label>
      <input id="pid" v-model="parkingId" type="text" autocomplete="off" />
    </div>
    <button type="button" class="btn-primary" aria-label="Definir" @click="setId">Definir</button>
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
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { setActiveParkingId } from '@/session/activeParking'
import { isValidParkingUuid } from '@/lib/uuidParking'
import { STRINGS } from '@/strings'

const router = useRouter()
const parkingId = ref('')
const hint = ref('')

function setId(): void {
  hint.value = ''
  const s = parkingId.value.trim()
  if (!isValidParkingUuid(s)) {
    hint.value = 'UUID inválido.'
    return
  }
  setActiveParkingId(s.toLowerCase())
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
</script>
