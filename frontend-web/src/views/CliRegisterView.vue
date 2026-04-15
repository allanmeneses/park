<template>
  <div class="page">
    <h1>Cadastro - Cliente</h1>

    <template v-if="invalidParkingLink">
      <p class="err">
        Este link de cadastro não é válido. Peça ao estacionamento um novo link (por exemplo por QR code, WhatsApp ou
        e-mail).
      </p>
      <p style="margin-top: 1rem">
        <RouterLink to="/login">Voltar ao login</RouterLink>
      </p>
    </template>

    <template v-else-if="!parkingFromRoute">
      <p class="hint">
        Para criar a sua conta precisa do <strong>link de cadastro</strong> que o estacionamento lhe enviar. Esse link já
        identifica onde estaciona — não precisa de nenhum código.
      </p>
      <p class="hint">Se não tiver o link, peça na receção ou ao responsável pelo estacionamento.</p>
      <p style="margin-top: 1rem">
        <RouterLink to="/login">Voltar ao login</RouterLink>
      </p>
    </template>

    <template v-else>
      <p class="hint">
        O estacionamento já foi identificado pelo link. Preencha a placa, o e-mail e a senha para criar a conta.
      </p>
      <form @submit.prevent="submit">
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
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, ref } from 'vue'
import axios from 'axios'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import { useAuthStore } from '@/stores/auth'
import { apiErrorMessage } from '@/lib/errors'
import { isValidPlateNormalized, normalizePlate } from '@/lib/plate'
import { isValidParkingUuid } from '@/lib/uuidParking'
import { STRINGS } from '@/strings'

const api = inject<AxiosInstance>('api')!
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const parkingFromRoute = computed(() => {
  const raw = route.params.parkingId
  if (typeof raw !== 'string' || !raw.trim()) return null
  const n = raw.trim().toLowerCase()
  return isValidParkingUuid(n) ? n : null
})

const invalidParkingLink = computed(() => {
  const raw = route.params.parkingId
  if (typeof raw !== 'string' || !raw.trim()) return false
  return parkingFromRoute.value === null
})

const plate = ref('')
const email = ref('')
const password = ref('')
const msg = ref('')
const plateErr = ref('')
const emailErr = ref('')
const passwordErr = ref('')

async function submit(): Promise<void> {
  msg.value = ''
  plateErr.value = ''
  emailErr.value = ''
  passwordErr.value = ''

  const pid = parkingFromRoute.value
  if (!pid) return

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
