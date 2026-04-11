import type { AxiosInstance } from 'axios'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import CliBuyView from './CliBuyView.vue'

const pushMock = vi.fn<(...args: unknown[]) => Promise<void>>()

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: pushMock,
  }),
}))

function createApiMock(): AxiosInstance {
  return {
    get: vi.fn().mockResolvedValue({
      data: {
        items: [
          {
            id: 'pkg-client-1',
            display_name: 'Cliente 10h',
            scope: 'CLIENT',
            hours: 10,
            price: '35,00',
            is_promo: true,
            sort_order: 10,
            active: true,
          },
        ],
      },
    }),
    post: vi.fn().mockResolvedValue({
      data: { payment_id: 'pay-123' },
    }),
  } as unknown as AxiosInstance
}

async function flushView(): Promise<void> {
  await Promise.resolve()
  await nextTick()
}

describe('CliBuyView', () => {
  afterEach(() => {
    pushMock.mockReset()
    vi.restoreAllMocks()
  })

  it('shows payment panel and submits PIX after selecting a package', async () => {
    const api = createApiMock()
    const confirmSpy = vi.spyOn(window, 'confirm')

    const wrapper = mount(CliBuyView, {
      global: {
        provide: { api },
      },
    })

    await flushView()

    expect(confirmSpy).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Cliente 10h')
    expect(wrapper.text()).not.toContain('crédito interno')
    expect(wrapper.text()).not.toContain('Forma de pagamento')

    await wrapper.get('button.pkg-button').trigger('click')
    await nextTick()

    expect(wrapper.text()).toContain('Forma de pagamento')
    expect(wrapper.text()).toContain('Pagar com PIX')
    expect(wrapper.text()).toContain('Pagar com cartão (em breve)')

    await wrapper.get('button[aria-label="Pagar com PIX"]').trigger('click')

    expect(api.post).toHaveBeenCalledWith(
      '/client/buy',
      { packageId: 'pkg-client-1', settlement: 'PIX' },
      { headers: { 'Idempotency-Key': expect.any(String) } },
    )
    expect(pushMock).toHaveBeenCalledWith('/cliente/pix/pay-123')
  })
})
