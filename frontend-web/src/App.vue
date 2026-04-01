<template>
  <header v-if="isLogged" class="topbar">
    <button type="button" class="btn-secondary" @click="logout">Sair</button>
  </header>
  <RouterView />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const auth = useAuthStore()

const isLogged = computed(() => !!auth.accessToken || !!sessionStorage.getItem('parking.v1.access'))

async function logout(): Promise<void> {
  try {
    const rt = auth.refreshToken || localStorage.getItem('parking.v1.refresh')
    if (auth.api && rt) await auth.api.post('/auth/logout', { refreshToken: rt })
  } catch {
    // best-effort logout; local cleanup still mandatory
  } finally {
    auth.clear()
    await router.push('/login')
  }
}
</script>

<style src="./style.css"></style>
