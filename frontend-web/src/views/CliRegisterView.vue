<template>
  <div class="page">
    <h1>Cadastro - Cliente</h1>
    <p class="hint">Informe o ID do estacionamento, a placa do veículo, seu e-mail e uma senha para criar a conta.</p>
    <form @submit.prevent="submit">
      <div class="field">
        <label for="parking">ID do estacionamento</label>
        <input
          id="parking"
          v-model="parkingId"
          type="text"
          autocomplete="off"
          aria-label="ID do estacionamento"
          @blur="parkingId = parkingId.trim().toLowerCase()"
        />
        <p v-if="parkingErr" class="err">{{ parkingErr }}</p>
      </div>
      <div class="field">
        <label for="plate">Placa do veículo</label>
        <input
          id="plate"
          v-model="plate"
          type="text"
          maxlength="10"
          autocomplete="off"
          aria-label="Placa do veículo"
          @blur="plate = normalizePlate(plate)"
        />
        <p v-if="plateErr" class="err">{{ plateErr }}</p>
      </div>
      <div class="field">
        <label for="email">E-mail</label>
        <input id="email" v-model="email" type="email" autocomplete="email" aria-label="E-mail" />
        <p v-if="emailErr" class="err">{{ emailErr }}</p>
      </div>
      <div class="field">
        <label for="password">Senha</label>
        <input
          id="password"
          v-model="password"
          type="password"
          autocomplete="new-password"
          aria-label="Senha"
        />
        <p v-if="passwordErr" class="err">{{ passwordErr }}</p>
      </div>
      <p v-if="msg" class="err">{{ msg }}</p>
      <button type="submit" class="btn-primary" :aria-label="STRINGS.B24">{{ STRINGS.B24 }}</button>
    </form>
    <p style="margin-top: 1rem">
      <RouterLink to="/login">Voltar ao login</RouterLink>
    </p>
  </div>
</template>

<script setup lang="ts">
import { inject, ref } from 'vue'
import axios from 'axios'
import { RouterLink, useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import { useAuthStore } from '@/stores/auth'
import { apiErrorMessage } from '@/lib/errors'
import { isValidPlateNormalized, normalizePlate } from '@/lib/plate'
import { STRINGS } from '@/strings'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const auth = useAuthStore()

const parkingId = ref('')
const plate = ref('')
const email = ref('')
const password = ref('')
const msg = ref('')
const parkingErr = ref('')
const plateErr = ref('')
const emailErr = ref('')
const passwordErr = ref('')

async function submit(): Promise<void> {
  msg.value = ''
  parkingErr.value = ''
  plateErr.value = ''
  emailErr.value = ''
  passwordErr.value = ''

  const pid = parkingId.value.trim().toLowerCase()
  parkingId.value = pid
  if (!pid) {
    parkingErr.value = STRINGS.E3
    return
  }

  const plateNorm = normalizePlate(plate.value)
  plate.value = plateNorm
  if (!isValidPlateNormalized(plateNorm)) {
    plateErr.value = 'Placa inválida.'
    return
  }

  if (!email.value.trim()) {
    emailErr.value = STRINGS.E3
    return
  }
  if (!password.value) {
    passwordErr.value = STRINGS.E3
    return
  }

  try {
    const { data } = await api.post<Record<string, unknown>>('/auth/register-client', {
      parkingId: pid,
      plate: plateNorm,
      email: email.value.trim(),
      password: password.value,
    })
    const at =
      (typeof data.access_token === 'string' && data.access_token) ||
      (typeof data.accessToken === 'string' && data.accessToken) ||
      ''
    const rt =
      (typeof data.refresh_token === 'string' && data.refresh_token) ||
      (typeof data.refreshToken === 'string' && data.refreshToken) ||
      ''
    const exp =
      (typeof data.expires_in === 'number' && data.expires_in) ||
      (typeof data.expiresIn === 'number' && data.expiresIn) ||
      28800
    if (!at || !rt) {
      msg.value = 'Resposta de cadastro inválida.'
      return
    }
    auth.setTokens(api, at, rt, exp)
    await router.replace('/cliente')
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) {
      msg.value = apiErrorMessage(e.response?.data, 'Falha no cadastro.')
      return
    }
    msg.value = 'Falha no cadastro.'
  }
}
</script>

<style scoped>
.hint {
  color: #757575;
  font-size: 0.9rem;
  margin-bottom: 1rem;
}
</style>
