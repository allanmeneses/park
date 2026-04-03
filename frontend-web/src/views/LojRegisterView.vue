<template>
  <div class="page">
    <h1>Cadastro — Lojista</h1>
    <p class="hint">Use o código de 10 caracteres e o código de ativação fornecidos pelo gestor.</p>
    <form @submit.prevent="submit">
      <div class="field">
        <label for="merchant">Código do lojista (10 caracteres)</label>
        <input
          id="merchant"
          v-model="merchantCode"
          type="text"
          maxlength="10"
          autocomplete="off"
          aria-label="Código do lojista"
          @blur="merchantCode = merchantCode.trim().toUpperCase()"
        />
        <p v-if="merchantErr" class="err">{{ merchantErr }}</p>
      </div>
      <div class="field">
        <label for="activation">Código de ativação</label>
        <input
          id="activation"
          v-model="activationCode"
          type="text"
          autocomplete="off"
          aria-label="Código de ativação"
        />
        <p v-if="activationErr" class="err">{{ activationErr }}</p>
      </div>
      <div class="field">
        <label for="name">Nome da loja</label>
        <input id="name" v-model="name" type="text" aria-label="Nome da loja" />
        <p v-if="nameErr" class="err">{{ nameErr }}</p>
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
import { STRINGS } from '@/strings'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const auth = useAuthStore()

const merchantCode = ref('')
const activationCode = ref('')
const name = ref('')
const email = ref('')
const password = ref('')
const msg = ref('')
const merchantErr = ref('')
const activationErr = ref('')
const nameErr = ref('')
const emailErr = ref('')
const passwordErr = ref('')

async function submit(): Promise<void> {
  msg.value = ''
  merchantErr.value = ''
  activationErr.value = ''
  nameErr.value = ''
  emailErr.value = ''
  passwordErr.value = ''

  const mc = merchantCode.value.trim().toUpperCase()
  merchantCode.value = mc
  if (mc.length !== 10) {
    merchantErr.value = STRINGS.E9
    return
  }
  if (!activationCode.value.trim()) {
    activationErr.value = STRINGS.E3
    return
  }
  if (!name.value.trim()) {
    nameErr.value = STRINGS.E3
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
    const { data } = await api.post<Record<string, unknown>>('/auth/register-lojista', {
      merchantCode: mc,
      activationCode: activationCode.value.trim(),
      name: name.value.trim(),
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
    await router.replace('/lojista')
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) {
      const data = e.response?.data
      const code = (data as { code?: string } | undefined)?.code
      msg.value = apiErrorMessage(data, 'Falha no cadastro.')
      if (code === 'LOJISTA_INVITE_INVALID' || code === 'LOJISTA_INVITE_CONSUMED') {
        /* message já mapeada */
      }
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
