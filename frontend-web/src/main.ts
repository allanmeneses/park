import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { router } from './router'
import { createApi } from './api/http'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)

const api = createApi()
const auth = useAuthStore()
auth.bindApi(api)
auth.loadFromStorage()
if (auth.accessToken && auth.refreshToken) {
  auth.scheduleRefresh(api)
}

app.use(router)
app.provide('api', api)

app.mount('#app')
