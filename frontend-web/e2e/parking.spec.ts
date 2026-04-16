import { randomUUID } from 'node:crypto'
import { test, expect } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'
import {
  apiOrigin,
  apiV1,
  pickParkingId,
  pickPaymentId,
  pickRefreshToken,
  pickToken,
  webhookSignature,
} from './helpers'

const SUPER_EMAIL = 'super@test.com'
const SUPER_PASSWORD = 'Super!12345'
const ADMIN_PASSWORD = 'Admin!12345'

let adminEmail: string
let adminAccessToken: string
let adminRefreshToken: string
let parkingId: string

async function loginTokens(
  request: APIRequestContext,
  email: string,
  password: string,
): Promise<{ access: string; refresh: string }> {
  const r = await request.post(`${apiV1}/auth/login`, {
    data: { email, password },
  })
  expect(r.ok(), await r.text()).toBeTruthy()
  const j = (await r.json()) as Record<string, unknown>
  return { access: pickToken(j), refresh: pickRefreshToken(j) }
}

test.describe.serial('SPEC_FRONTEND §13.2 — E2E', () => {
  test.beforeAll(async ({ request }) => {
    const health = await request.get(`${apiOrigin}/health`)
    expect(health.ok(), 'API /health deve responder').toBeTruthy()

    const superTok = (await loginTokens(request, SUPER_EMAIL, SUPER_PASSWORD))
      .access
    adminEmail = `e2e_admin_${Date.now()}@test.local`
    const operatorEmail = `e2e_op_${Date.now()}@test.local`
    const tr = await request.post(`${apiV1}/admin/tenants`, {
      headers: { Authorization: `Bearer ${superTok}` },
      data: {
        parkingId: null,
        adminEmail,
        adminPassword: ADMIN_PASSWORD,
        operatorEmail,
        operatorPassword: 'Op!12345',
      },
    })
    expect(tr.ok(), await tr.text()).toBeTruthy()
    const tj = (await tr.json()) as Record<string, unknown>
    parkingId = pickParkingId(tj)
    const pair = await loginTokens(request, adminEmail, ADMIN_PASSWORD)
    adminAccessToken = pair.access
    adminRefreshToken = pair.refresh
  })

  test('login → shell do perfil (gestor)', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('E-mail').fill(adminEmail)
    await page.getByLabel('Senha').fill(ADMIN_PASSWORD)
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page).toHaveURL(/\/gestor/)
  })

  test('cadastro lojista com convite leva à carteira', async ({
    page,
    request,
  }) => {
    const ir = await request.post(`${apiV1}/admin/lojista-invites`, {
      headers: { Authorization: `Bearer ${adminAccessToken}` },
      data: { displayName: 'Loja E2E Reg' },
    })
    expect(ir.ok(), await ir.text()).toBeTruthy()
    const inv = (await ir.json()) as {
      merchantCode?: string
      activationCode?: string
    }
    const mc = inv.merchantCode
    const ac = inv.activationCode
    expect(mc).toBeTruthy()
    expect(ac).toBeTruthy()
    const regEmail = `e2e_loj_reg_ui_${Date.now()}@test.local`
    await page.goto('/cadastro/lojista')
    await page.getByLabel('Código do lojista').fill(mc!)
    await page.getByLabel('Código de ativação').fill(ac!)
    await page.getByLabel('Nome da loja').fill('Loja Playwright')
    await page.getByLabel('E-mail', { exact: true }).fill(regEmail)
    await page.getByLabel('Senha', { exact: true }).fill('LojReg!12345')
    await page.getByRole('button', { name: 'Criar conta' }).click()
    await expect(page).toHaveURL(/\/lojista/, { timeout: 30_000 })
  })

  test('cadastro cliente leva à carteira', async ({ page }) => {
    const regEmail = `e2e_cli_reg_ui_${Date.now()}@test.local`
    await page.goto(`/cadastro/cliente/${parkingId}`)
    await page.getByLabel('Placa do veículo').fill('ABC1D23')
    await page.getByLabel('E-mail', { exact: true }).fill(regEmail)
    await page.getByLabel('Senha', { exact: true }).fill('CliReg!12345')
    await page.getByRole('button', { name: 'Criar conta' }).click()
    await expect(page).toHaveURL(/\/cliente/, { timeout: 30_000 })
  })

  test.describe('com sessão (novo contexto por teste)', () => {
    test.beforeEach(async ({ page }) => {
      await page.addInitScript(
        (tok: { a: string; r: string }) => {
          sessionStorage.setItem('parking.v1.access', tok.a)
          localStorage.setItem('parking.v1.refresh', tok.r)
        },
        { a: adminAccessToken, r: adminRefreshToken },
      )
    })

    test('operador: nova entrada aparece na lista', async ({ page }) => {
      const n = Math.floor(Math.random() * 9000) + 1000
      // Formato legado ABC9999 (E2E9999 é inválido: o 2 não é letra).
      const plate = `ABC${n}`
      await page.goto('/operador/entrada')
      await page.getByLabel('Placa do veículo').fill(plate)
      const created = page.waitForResponse(
        (r) =>
          r.request().method() === 'POST' &&
          r.url().includes('/api/v1/tickets') &&
          !r.url().includes('checkout') &&
          r.ok(),
        { timeout: 30_000 },
      )
      const openList = page.waitForResponse(
        (r) => r.url().includes('/tickets/open') && r.ok(),
        { timeout: 30_000 },
      )
      await page.getByRole('button', { name: 'Confirmar' }).click()
      await created
      await expect(page).toHaveURL(/\/operador/)
      await openList
      await expect(page.getByText(plate, { exact: true })).toBeVisible({
        timeout: 30_000,
      })
    })

    test('operador: botão Gestão abre painel (SPEC §6)', async ({ page }) => {
      await page.goto('/operador')
      await expect(page.getByRole('button', { name: 'Gestão' })).toBeVisible()
      await page.getByRole('button', { name: 'Gestão' }).click()
      await expect(page).toHaveURL(/\/gestor/)
      await expect(page.getByRole('heading', { name: 'Painel' })).toBeVisible()
    })

    test('PIX + webhook: ticket encerrado (API + UI)', async ({
      page,
      request,
    }) => {
    const n = Math.floor(Math.random() * 9000) + 1000
    const plate = `PXE${n}`
    const idem = randomUUID()
    const cr = await request.post(`${apiV1}/tickets`, {
      headers: {
        Authorization: `Bearer ${adminAccessToken}`,
        'Idempotency-Key': idem,
      },
      data: { plate },
    })
    expect(cr.ok(), await cr.text()).toBeTruthy()
    const ticket = (await cr.json()) as { id?: string }
    expect(ticket.id).toBeTruthy()
    const ticketId = ticket.id!

    const exitIso = new Date(Date.now() + 3 * 60 * 60 * 1000).toISOString()
    const co = await request.post(`${apiV1}/tickets/${ticketId}/checkout`, {
      headers: {
        Authorization: `Bearer ${adminAccessToken}`,
        'Idempotency-Key': randomUUID(),
      },
      data: { exit_time: exitIso },
    })
    expect(co.ok(), await co.text()).toBeTruthy()
    const coj = (await co.json()) as Record<string, unknown>
    const paymentId = pickPaymentId(coj)
    expect(Number(String(coj.amount ?? '0').replace(',', '.'))).toBeGreaterThan(0)

    const pix = await request.post(`${apiV1}/payments/pix`, {
      headers: { Authorization: `Bearer ${adminAccessToken}` },
      data: { payment_id: paymentId },
    })
    expect(pix.ok(), await pix.text()).toBeTruthy()

    const secret =
      process.env.PIX_WEBHOOK_SECRET ?? 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
    const tx = randomUUID().replace(/-/g, '')
    const raw = JSON.stringify({
      transaction_id: tx,
      payment_id: paymentId,
      status: 'PAID',
    })
    const sig = webhookSignature(raw, secret)
    const wh = await fetch(`${apiV1}/payments/webhook`, {
      method: 'POST',
      headers: {
        'X-Parking-Id': parkingId,
        'X-Signature': sig,
        'Content-Type': 'application/json',
      },
      body: raw,
    })
    const wht = await wh.text()
    if (!wh.ok) throw new Error(`webhook ${wh.status}: ${wht}`)

    const gr = await request.get(`${apiV1}/tickets/${ticketId}`, {
      headers: { Authorization: `Bearer ${adminAccessToken}` },
    })
    expect(gr.ok()).toBeTruthy()
    const gj = (await gr.json()) as {
      ticket?: { status?: string; plate?: string }
    }
    expect(gj.ticket?.status).toBe('CLOSED')

    await page.goto(`/operador/ticket/${ticketId}`)
    await expect(page.getByText('CLOSED')).toBeVisible()
    })
  })

  test.describe('cliente — histórico (seed E2E)', () => {
    let cliEmail: string
    let cliAccess: string
    let cliRefresh: string

    test.beforeAll(async ({ request }) => {
      cliEmail = `e2e_cli_ui_${Date.now()}@test.local`
      const seed = await request.post(`${apiV1}/admin/e2e/client-with-history`, {
        headers: { Authorization: `Bearer ${adminAccessToken}` },
        data: { email: cliEmail, password: 'Cli!12345' },
      })
      expect(seed.ok(), await seed.text()).toBeTruthy()
      const pair = await loginTokens(request, cliEmail, 'Cli!12345')
      cliAccess = pair.access
      cliRefresh = pair.refresh
    })

    test.beforeEach(async ({ page }) => {
      await page.addInitScript(
        (tok: { a: string; r: string }) => {
          sessionStorage.setItem('parking.v1.access', tok.a)
          localStorage.setItem('parking.v1.refresh', tok.r)
        },
        { a: cliAccess, r: cliRefresh },
      )
    })

    test('histórico exibe Compra e Uso formatados', async ({ page }) => {
      await page.goto('/cliente/historico')
      await expect(page.getByText('Compra')).toBeVisible({ timeout: 30_000 })
      await expect(page.getByText('Uso')).toBeVisible()
      await expect(page.getByText(/\+\d+\s*h/)).toBeVisible()
      await expect(page.getByText(/R\$\s*\d/)).toBeVisible()
    })
  })

  test.describe('lojista — histórico (seed E2E)', () => {
    let lojEmail: string
    let lojAccess: string
    let lojRefresh: string

    test.beforeAll(async ({ request }) => {
      lojEmail = `e2e_loj_ui_${Date.now()}@test.local`
      const seed = await request.post(`${apiV1}/admin/e2e/lojista-with-history`, {
        headers: { Authorization: `Bearer ${adminAccessToken}` },
        data: { email: lojEmail, password: 'Loj!12345' },
      })
      expect(seed.ok(), await seed.text()).toBeTruthy()
      const pair = await loginTokens(request, lojEmail, 'Loj!12345')
      lojAccess = pair.access
      lojRefresh = pair.refresh
    })

    test.beforeEach(async ({ page }) => {
      await page.addInitScript(
        (tok: { a: string; r: string }) => {
          sessionStorage.setItem('parking.v1.access', tok.a)
          localStorage.setItem('parking.v1.refresh', tok.r)
        },
        { a: lojAccess, r: lojRefresh },
      )
    })

    test('histórico exibe Compra e Uso formatados', async ({ page }) => {
      await page.goto('/lojista/historico')
      await expect(page.getByText('Compra')).toBeVisible({ timeout: 30_000 })
      await expect(page.getByText('Uso')).toBeVisible()
      await expect(page.getByText(/\+\d+\s*h/)).toBeVisible()
      await expect(page.getByText(/R\$\s*\d/)).toBeVisible()
    })
  })
})
