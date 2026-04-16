<template>
  <div v-if="clockBlocked" class="clock-sync-block" role="alert">
    <p class="clock-sync-msg">{{ STRINGS.S25 }}</p>
  </div>
  <template v-else>
    <header v-if="isLogged" class="topbar">
      <ParkingMark size="sm" class="topbar-mark" />
      <button type="button" class="btn-secondary" @click="logout">Sair</button>
    </header>
    <RouterView v-slot="{ Component }">
      <Transition name="view-fade" mode="out-in">
        <component :is="Component" />
      </Transition>
    </RouterView>
  </template>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useClockSyncStore } from '@/stores/clockSync'
import ParkingMark from '@/components/ParkingMark.vue'
import { STRINGS } from '@/strings'

const router = useRouter()
const auth = useAuthStore()
const clock = useClockSyncStore()

let stopClockListeners: (() => void) | undefined

const isLogged = computed(() => !!auth.accessToken || !!sessionStorage.getItem('parking.v1.access'))

const clockBlocked = computed(() => {
  if (typeof navigator !== 'undefined' && !navigator.onLine) return false
  return clock.blockWhenOnline
})

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

onMounted(() => {
  stopClockListeners = clock.registerListeners()
})

onUnmounted(() => {
  stopClockListeners?.()
})
</script>

<style src="./style.css"></style>
