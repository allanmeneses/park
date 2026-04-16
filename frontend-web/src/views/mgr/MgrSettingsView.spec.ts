import type { AxiosInstance } from 'axios'
import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { STORAGE_ACCESS } from '@/api/http'
import MgrSettingsView from './MgrSettingsView.vue'

const testParkingId = 'f0000000-0000-4000-8000-000000000001'

function makeJwt(role: string, parkingId?: string): string {
  const body: Record<string, unknown> = {
    exp: Math.floor(Date.now() / 1000) + 3600,
    role,
  }
  if (parkingId) body.parking_id = parkingId
  const payload = btoa(JSON.stringify(body))
  return `x.${payload}.y`
}

async function flushView(): Promise<void> {
  await Promise.resolve()
  await nextTick()
  await Promise.resolve()
  await nextTick()
}

describe('MgrSettingsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    sessionStorage.setItem(STORAGE_ACCESS, makeJwt('ADMIN', testParkingId))
    vi.spyOn(window, 'alert').mockImplementation(() => undefined)
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
    })
  })

  afterEach(() => {
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('loads grant validity config and audit log, then sends the new flag on save', async () => {
    const post = vi.fn().mockResolvedValue({ data: { ok: true } })
    const api = {
      get: vi.fn(async (url: string) => {
        if (url === '/settings') {
          return {
            data: {
              price_per_hour: '5.00',
              capacity: 50,
              lojista_grant_same_day_only: true,
            },
          }
        }
        if (url === '/settings/audit') {
          return {
            data: {
              items: [
                {
                  id: '1',
                  created_at: '2026-04-11T12:00:00Z',
                  actor_email: 'admin@test.com',
                  actor_role: 'ADMIN',
                  changes: [
                    {
                      field: 'lojista_grant_same_day_only',
                      label: 'Validade da bonificação do lojista',
                      from: 'Prazo indeterminado',
                      to: 'Somente no dia da bonificação',
                    },
                  ],
                },
              ],
            },
          }
        }
        if (url === '/recharge-packages/manage?scope=CLIENT' || url === '/recharge-packages/manage?scope=LOJISTA') {
          return { data: { items: [] } }
        }
        throw new Error(`unexpected GET ${url}`)
      }),
      post,
      put: vi.fn(),
      delete: vi.fn(),
    } as unknown as AxiosInstance

    const wrapper = mount(MgrSettingsView, {
      global: {
        provide: { api },
        stubs: {
          MgrLojistaInvitesSection: true,
          RouterLink: true,
        },
      },
    })

    await flushView()

    expect(wrapper.text()).toContain('Cadastro de clientes')
    const regInput = wrapper.get('#client-register-url').element as HTMLInputElement
    expect(regInput.value).toContain(`/cadastro/cliente/${testParkingId}`)

    const validity = wrapper.get('#lojista-grant-same-day-only')
    expect((validity.element as HTMLInputElement).checked).toBe(true)
    expect(wrapper.text()).toContain('Histórico de alterações')
    expect(wrapper.text()).toContain('admin@test.com')
    expect(wrapper.text()).toContain('Prazo indeterminado')
    expect(wrapper.text()).toContain('Somente no dia da bonificação')

    await validity.setValue(false)
    await wrapper.get('button[aria-label="Salvar"]').trigger('click')

    expect(post).toHaveBeenCalledWith('/settings', {
      pricePerHour: 5,
      capacity: 50,
      lojistaGrantSameDayOnly: false,
    })
  })

  it('copies client registration link to clipboard', async () => {
    const api = {
      get: vi.fn(async (url: string) => {
        if (url === '/settings') {
          return {
            data: {
              price_per_hour: '5.00',
              capacity: 50,
              lojista_grant_same_day_only: false,
            },
          }
        }
        if (url === '/settings/audit') {
          return { data: { items: [] } }
        }
        if (url === '/recharge-packages/manage?scope=CLIENT' || url === '/recharge-packages/manage?scope=LOJISTA') {
          return { data: { items: [] } }
        }
        throw new Error(`unexpected GET ${url}`)
      }),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as unknown as AxiosInstance

    const wrapper = mount(MgrSettingsView, {
      global: {
        provide: { api },
        stubs: {
          MgrLojistaInvitesSection: true,
          RouterLink: true,
        },
      },
    })

    await flushView()

    await wrapper.get('[aria-label="Copiar link de cadastro"]').trigger('click')
    await flushView()

    expect(navigator.clipboard.writeText).toHaveBeenCalled()
    const copied = (navigator.clipboard.writeText as ReturnType<typeof vi.fn>).mock.calls[0][0] as string
    expect(copied).toContain(`/cadastro/cliente/${testParkingId}`)
    expect(wrapper.text()).toContain('Link copiado')
  })

  it('keeps the grant validity field disabled for manager', async () => {
    sessionStorage.setItem(STORAGE_ACCESS, makeJwt('MANAGER', testParkingId))
    const api = {
      get: vi.fn(async (url: string) => {
        if (url === '/settings') {
          return {
            data: {
              price_per_hour: '5.00',
              capacity: 50,
              lojista_grant_same_day_only: false,
            },
          }
        }
        if (url === '/settings/audit') {
          return { data: { items: [] } }
        }
        if (url === '/recharge-packages?scope=CLIENT' || url === '/recharge-packages?scope=LOJISTA') {
          return { data: { items: [] } }
        }
        throw new Error(`unexpected GET ${url}`)
      }),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    } as unknown as AxiosInstance

    const wrapper = mount(MgrSettingsView, {
      global: {
        provide: { api },
        stubs: {
          MgrLojistaInvitesSection: true,
          RouterLink: true,
        },
      },
    })

    await flushView()

    expect(wrapper.get('#lojista-grant-same-day-only').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Cadastro de clientes')
  })
})
