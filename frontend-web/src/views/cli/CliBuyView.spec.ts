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

function createApiMock(price = '35,00'): AxiosInstance {
  const post = vi
    .fn()
    .mockResolvedValueOnce({ data: { payment_id: 'pay-pix-123' } })
    .mockResolvedValueOnce({ data: { payment_id: 'pay-card-123' } })

  return {
    get: vi.fn().mockResolvedValue({
      data: {
        items: [
          {
            id: 'pkg-client-1',
            display_name: 'Cliente 10h',
            scope: 'CLIENT',
            hours: 10,
            price,
            is_promo: true,
            sort_order: 10,
            active: true,
          },
        ],
      },
    }),
    post,
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

  it('shows payment panel and submits PIX and card after selecting a package', async () => {
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
    expect(wrapper.text()).toContain('Pagar com cartão')

    await wrapper.get('button[aria-label="Pagar com PIX"]').trigger('click')

    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/client/buy',
      { packageId: 'pkg-client-1', settlement: 'PIX' },
      { headers: { 'Idempotency-Key': expect.any(String) } },
    )
    expect(pushMock).toHaveBeenNthCalledWith(1, '/cliente/pix/pay-pix-123')

    await wrapper.get('button[aria-label="Pagar com cartão"]').trigger('click')

    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/client/buy',
      { packageId: 'pkg-client-1', settlement: 'CARD' },
      { headers: { 'Idempotency-Key': expect.any(String) } },
    )
    expect(pushMock).toHaveBeenNthCalledWith(2, '/cliente/cartao/pay-card-123')
  })

  it('bloqueia cartão com mensagem amigável abaixo do mínimo do Mercado Pago', async () => {
    const api = createApiMock('0,06')

    const wrapper = mount(CliBuyView, {
      global: {
        provide: { api },
      },
    })

    await flushView()
    await wrapper.get('button.pkg-button').trigger('click')
    await nextTick()

    const cardButton = wrapper.get('button[aria-label="Pagar com cartão"]')
    expect(cardButton.attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Pagamento com cartão disponível apenas para valores a partir de R$ 1,00.')
    expect(wrapper.text()).toContain('Para este pacote, use PIX.')

    await cardButton.trigger('click')
    expect(api.post).not.toHaveBeenCalled()
    expect(pushMock).not.toHaveBeenCalled()
  })
})
