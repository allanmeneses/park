<template>
  <div class="page">
    <h1>PIX</h1>
    <p v-if="err" class="err">{{ err }}</p>
    <img v-if="img" :src="img" width="280" height="280" alt="" />
    <p v-if="remaining > 0">Expira em {{ remaining }}s</p>
    <p v-else-if="expired">QR expirado.</p>
    <button type="button" class="btn-primary" aria-label="Copiar cÃ³digo PIX" @click="copy">Copiar cÃ³digo PIX</button>
    <button type="button" class="btn-primary" style="margin-left: 0.5rem" aria-label="Gerar novo QR" @click="loadQr">
      Gerar novo QR
    </button>
    <button type="button" style="margin-top: 1rem; display: block" aria-label="InÃ­cio" @click="$router.replace('/operador')">InÃ­cio</button>
  </div>
</template>

<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { AxiosInstance } from 'axios'
import QRCode from 'qrcode'
import { str } from '@/lib/apiDto'
import { normalizePaymentStatus } from '@/lib/paymentStatus'

const props = defineProps<{ paymentId: string }>()
const api = inject<AxiosInstance>('api')!
const router = useRouter()

const img = ref('')
const err = ref('')
const qrText = ref('')
const remaining = ref(0)
const expired = ref(false)
let pollTimer: ReturnType<typeof setInterval> | null = null
let expTimer: ReturnType<typeof setInterval> | null = null
let started = 0
let checking = false

function clearTimers(): void {
  if (pollTimer) clearInterval(pollTimer)
  if (expTimer) clearInterval(expTimer)
  pollTimer = null
  expTimer = null
}

async function checkPaymentStatus(): Promise<void> {
  if (checking) return
  checking = true
  try {
    const { data } = await api.get<Record<string, unknown>>(`/payments/${props.paymentId}`)
    const st = normalizePaymentStatus(data.status ?? data.Status)
    if (st === 'PAID') {
      clearTimers()
      await router.replace('/operador')
    } else if (st === 'EXPIRED') {
      expired.value = true
    } else if (st === 'FAILED') {
      clearTimers()
      err.value = 'Pagamento falhou. Escolha outro metodo ou tente novamente.'
    }
  } catch {
    /* ignore */
  } finally {
    checking = false
  }
}

async function loadQr(): Promise<void> {
  err.value = ''
  expired.value = false
  try {
    const { data } = await api.post<Record<string, unknown>>('/payments/pix', {
      payment_id: props.paymentId,
    })
    const qr = str(data.qr_code ?? data.qrCode)
    qrText.value = qr
    img.value = await QRCode.toDataURL(qr, { width: 280, margin: 2 })
    const exp = data.expires_at ?? data.expiresAt
    const expMs = exp ? new Date(String(exp)).getTime() : 0
    if (expTimer) clearInterval(expTimer)
    expTimer = setInterval(() => {
      const left = Math.max(0, Math.floor((expMs - Date.now()) / 1000))
      remaining.value = left
      if (left <= 0) {
        expired.value = true
        if (expTimer) clearInterval(expTimer)
      }
    }, 1000)
  } catch {
    err.value = 'Falha ao gerar PIX.'
  }
}

async function poll(): Promise<void> {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = setInterval(async () => {
    if (Date.now() - started > 900_000) {
      clearTimers()
      err.value = 'Tempo limite de espera do pagamento. Use Gerar novo QR.'
      return
    }
    await checkPaymentStatus()
  }, 2000)
}

function onWindowFocus(): void {
  void checkPaymentStatus()
}

function onVisibilityChange(): void {
  if (document.visibilityState === 'visible') {
    void checkPaymentStatus()
  }
}

async function copy(): Promise<void> {
  if (!qrText.value) return
  try {
    await navigator.clipboard.writeText(qrText.value)
    alert('Código copiado.')
  } catch {
    err.value = 'Não foi possível copiar.'
  }
}

onMounted(() => {
  started = Date.now()
  void loadQr().then(async () => {
    await checkPaymentStatus()
    await poll()
  })
  window.addEventListener('focus', onWindowFocus)
  document.addEventListener('visibilitychange', onVisibilityChange)
})

onUnmounted(() => {
  clearTimers()
  window.removeEventListener('focus', onWindowFocus)
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>


