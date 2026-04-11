import type { AxiosInstance } from 'axios'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import AdmTenantView from './AdmTenantView.vue'

const pushMock = vi.fn<(...args: unknown[]) => Promise<void>>()
const getActiveParkingIdMock = vi.fn<() => string | null>()
const setActiveParkingIdMock = vi.fn<(id: string | null) => void>()

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: pushMock,
  }),
}))

vi.mock('@/session/activeParking', () => ({
  getActiveParkingId: () => getActiveParkingIdMock(),
  setActiveParkingId: (id: string | null) => setActiveParkingIdMock(id),
}))

async function flushView(): Promise<void> {
  await Promise.resolve()
  await nextTick()
}

describe('AdmTenantView', () => {
  afterEach(() => {
    pushMock.mockReset()
    getActiveParkingIdMock.mockReset()
    setActiveParkingIdMock.mockReset()
  })

  it('shows list load error and keeps technical UUID in advanced section', async () => {
    getActiveParkingIdMock.mockReturnValue(null)
    const api = {
      get: vi.fn().mockRejectedValue(new Error('offline')),
      post: vi.fn(),
    } as unknown as AxiosInstance

    const wrapper = mount(AdmTenantView, {
      global: {
        provide: { api },
      },
    })

    await flushView()

    expect(wrapper.text()).toContain('Não foi possível carregar a lista de estacionamentos.')
    expect(wrapper.find('details.tech').exists()).toBe(true)
    expect(wrapper.find('summary').text()).toContain('Identificador técnico')
  })

  it('validates manual UUID and stores selected tenant in lowercase', async () => {
    getActiveParkingIdMock.mockReturnValue(null)
    const api = {
      get: vi.fn().mockResolvedValue({
        data: {
          items: [
            {
              parkingId: '550E8400-E29B-41D4-A716-446655440000',
              label: 'Tenant A',
            },
          ],
        },
      }),
      post: vi.fn(),
    } as unknown as AxiosInstance

    const wrapper = mount(AdmTenantView, {
      global: {
        provide: { api },
      },
    })

    await flushView()

    await wrapper.get('#pid').setValue('abc')
    await wrapper.get('button[aria-label="Definir"]').trigger('click')
    expect(wrapper.text()).toContain('UUID inválido.')

    await wrapper.get('#tenant-select').setValue('550e8400-e29b-41d4-a716-446655440000')
    expect(setActiveParkingIdMock).toHaveBeenLastCalledWith('550e8400-e29b-41d4-a716-446655440000')
  })
})
