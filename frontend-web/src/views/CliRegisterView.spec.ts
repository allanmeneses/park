import type { AxiosInstance } from 'axios'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createRouter, createMemoryHistory } from 'vue-router'
import { defineComponent, h } from 'vue'
import CliRegisterView from './CliRegisterView.vue'

const validParking = 'f0000000-0000-4000-8000-000000000001'

const LoginStub = defineComponent({ name: 'LoginStub', setup: () => () => h('div') })

describe('CliRegisterView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  async function makeRouter(initialPath: string) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/login', name: 'login', component: LoginStub },
        {
          path: '/cadastro/cliente/:parkingId?',
          name: 'cli_register',
          component: CliRegisterView,
        },
      ],
    })
    await router.push(initialPath)
    await router.isReady()
    return router
  }

  it('shows registration form only when URL contains a valid parking UUID', async () => {
    const router = await makeRouter(`/cadastro/cliente/${validParking}`)
    const post = vi.fn()
    const wrapper = mount(CliRegisterView, {
      global: {
        plugins: [router],
        provide: { api: { post } as unknown as AxiosInstance },
      },
      attachTo: document.body,
    })
    await flushPromises()
    expect(wrapper.find('#plate').exists()).toBe(true)
    expect(wrapper.find('form').exists()).toBe(true)
    wrapper.unmount()
  })

  it('shows instructions without form when route has no parking segment', async () => {
    const router = await makeRouter('/cadastro/cliente')
    const post = vi.fn()
    const wrapper = mount(CliRegisterView, {
      global: {
        plugins: [router],
        provide: { api: { post } as unknown as AxiosInstance },
      },
      attachTo: document.body,
    })
    await flushPromises()
    expect(wrapper.text()).toContain('link de cadastro')
    expect(wrapper.find('#plate').exists()).toBe(false)
    expect(wrapper.find('form').exists()).toBe(false)
    wrapper.unmount()
  })

  it('shows invalid link message without form when parking segment is not a valid UUID', async () => {
    const router = await makeRouter('/cadastro/cliente/not-a-uuid')
    const post = vi.fn()
    const wrapper = mount(CliRegisterView, {
      global: {
        plugins: [router],
        provide: { api: { post } as unknown as AxiosInstance },
      },
      attachTo: document.body,
    })
    await flushPromises()
    expect(wrapper.text()).toContain('não é válido')
    expect(wrapper.find('#plate').exists()).toBe(false)
    expect(wrapper.find('form').exists()).toBe(false)
    wrapper.unmount()
  })
})
