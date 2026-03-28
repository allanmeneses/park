<template>
  <div class="page">
    <h1>Estacionamento</h1>
    <form @submit.prevent="submit">
      <div class="field">
        <label for="email">E-mail</label>
        <input
          id="email"
          v-model="email"
          type="email"
          autocomplete="username"
          aria-label="E-mail"
        />
        <p v-if="fieldErr" class="err">{{ fieldErr }}</p>
      </div>
      <div class="field">
        <label for="password">Senha</label>
        <input
          id="password"
          v-model="password"
          type="password"
          autocomplete="current-password"
          aria-label="Senha"
        />
      </div>
      <p v-if="msg" class="err">{{ msg }}</p>
      <button type="submit" class="btn-primary" :aria-label="STRINGS.B1">{{ STRINGS.B1 }}</button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { inject, ref } from 'vue'
import axios from 'axios'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import { useAuthStore } from '@/stores/auth'
import { apiErrorMessage } from '@/lib/errors'
import { STRINGS } from '@/strings'

const api = inject<AxiosInstance>('api')!
const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const msg = ref('')
const fieldErr = ref('')

function homeForRole(role: string | null): string {
  if (role === 'OPERATOR') return '/operador'
  if (role === 'MANAGER' || role === 'ADMIN') return '/gestor'
  if (role === 'CLIENT') return '/cliente'
  if (role === 'LOJISTA') return '/lojista'
  if (role === 'SUPER_ADMIN') return '/admin/tenant'
  return '/login'
}

async function submit(): Promise<void> {
  msg.value = ''
  fieldErr.value = ''
  if (!email.value.trim()) {
    fieldErr.value = STRINGS.E3
    return
  }
  if (!password.value) {
    fieldErr.value = STRINGS.E3
    return
  }
  try {
    const { data } = await api.post<Record<string, unknown>>('/auth/login', {
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
      msg.value = 'Resposta de login inválida.'
      return
    }
    auth.setTokens(api, at, rt, exp)
    await router.replace(homeForRole(auth.role))
  } catch (e: unknown) {
    if (axios.isAxiosError(e)) {
      const code = (e.response?.data as { code?: string } | undefined)?.code
      if (code === 'OPERATOR_BLOCKED') {
        msg.value =
          'Operador bloqueado. Procure o gestor.'
        return
      }
      if (code === 'LOGIN_THROTTED') {
        msg.value = 'Aguarde antes de tentar de novo.'
        return
      }
      msg.value = apiErrorMessage(e.response?.data, 'Falha no login.')
      return
    }
    msg.value = 'Falha no login.'
  }
}
</script>
