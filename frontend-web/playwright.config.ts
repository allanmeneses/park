import { existsSync, readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig, devices } from '@playwright/test'

const configDir = dirname(fileURLToPath(import.meta.url))

/** Alinha HMAC do webhook E2E com a API local (mesmo `PIX_WEBHOOK_SECRET` do `.env` na raiz do repo). */
function loadPixWebhookSecretFromRootEnv(): void {
  if (process.env.PIX_WEBHOOK_SECRET) return
  const envPath = resolve(configDir, '..', '.env')
  if (!existsSync(envPath)) return
  for (const line of readFileSync(envPath, 'utf8').split('\n')) {
    const t = line.trim()
    if (!t || t.startsWith('#')) continue
    const i = t.indexOf('=')
    if (i < 1) continue
    const name = t.slice(0, i).trim()
    if (name !== 'PIX_WEBHOOK_SECRET') continue
    process.env.PIX_WEBHOOK_SECRET = t.slice(i + 1).trim()
    break
  }
}

loadPixWebhookSecretFromRootEnv()

const apiBase =
  process.env.E2E_API_BASE ?? 'http://127.0.0.1:8080/api/v1'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 20_000 },
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://127.0.0.1:5173',
    trace: 'on-first-retry',
    ...devices['Desktop Chrome'],
  },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 5173 --strictPort',
    url: 'http://127.0.0.1:5173',
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: {
      ...process.env,
      VITE_API_BASE: apiBase,
    },
  },
})
