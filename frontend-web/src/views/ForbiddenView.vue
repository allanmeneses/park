<template>
  <div class="page">
    <h1>{{ STRINGS.S9 }}</h1>
    <p>{{ STRINGS.S10 }}</p>
    <button type="button" class="btn-primary" :aria-label="STRINGS.B11" @click="go">
      {{ STRINGS.B11 }}
    </button>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { STRINGS } from '@/strings'

const router = useRouter()
const auth = useAuthStore()

function go(): void {
  auth.loadFromStorage()
  const r = auth.role
  if (r === 'OPERATOR') router.replace('/operador')
  else if (r === 'MANAGER' || r === 'ADMIN') router.replace('/gestor')
  else if (r === 'CLIENT') router.replace('/cliente')
  else if (r === 'LOJISTA') router.replace('/lojista')
  else if (r === 'SUPER_ADMIN') router.replace('/admin/tenant')
  else router.replace('/login')
}
</script>
