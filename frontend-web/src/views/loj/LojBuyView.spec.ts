import type { AxiosInstance } from 'axios'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import LojBuyView from './LojBuyView.vue'

const pushMock = vi.fn<(...args: unknown[]) => Promise<void>>()

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: pushMock,
  }),
}))

function createApiMock(): AxiosInstance {
  const post = vi
    .fn()
    .mockResolvedValueOnce({ data: { payment_id: 'pix-123' } })
    .mockResolvedValueOnce({ data: { payment_id: 'card-123' } })

  return {
    get: vi.fn().mockResolvedValue({
      data: {
        items: [
          {
            id: 'pkg-loj-1',
            display_name: 'Convênio 20h',
            scope: 'LOJISTA',
            hours: 20,
            price: '60,00',
            is_promo: false,
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

describe('LojBuyView', () => {
  afterEach(() => {
    pushMock.mockReset()
    vi.restoreAllMocks()
  })

  it('submete PIX e cartão para o lojista', async () => {
    const api = createApiMock()

    const wrapper = mount(LojBuyView, {
      global: {
        provide: { api },
      },
    })

    await flushView()
    await wrapper.get('button.pkg-button').trigger('click')
    await nextTick()

    expect(wrapper.text()).toContain('Pagar com PIX')
    expect(wrapper.text()).toContain('Pagar com cartão')

    await wrapper.get('button[aria-label="Pagar com PIX"]').trigger('click')
    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/lojista/buy',
      { packageId: 'pkg-loj-1', settlement: 'PIX' },
      { headers: { 'Idempotency-Key': expect.any(String) } },
    )
    expect(pushMock).toHaveBeenNthCalledWith(1, '/lojista/pix/pix-123')

    await wrapper.get('button[aria-label="Pagar com cartão"]').trigger('click')
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/lojista/buy',
      { packageId: 'pkg-loj-1', settlement: 'CARD' },
      { headers: { 'Idempotency-Key': expect.any(String) } },
    )
    expect(pushMock).toHaveBeenNthCalledWith(2, '/lojista/cartao/card-123')
  })
})
