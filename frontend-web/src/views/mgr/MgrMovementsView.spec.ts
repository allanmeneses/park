import type { AxiosInstance } from 'axios'
import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import MgrMovementsView from './MgrMovementsView.vue'

function createApiMock(): AxiosInstance {
  return {
    get: vi.fn().mockResolvedValue({
      data: {
        from: '2026-04-01T00:00:00Z',
        to: '2026-04-08T00:00:00Z',
        count: 0,
        insights: {
          total_ticket: '0.00',
          total_package: '0.00',
          usages_lojista: 0,
          usages_client: 0,
        },
        items: [],
      },
    }),
  } as unknown as AxiosInstance
}

async function flushView(): Promise<void> {
  await Promise.resolve()
  await nextTick()
}

describe('MgrMovementsView', () => {
  it('offers the same kind options and sends selected kind to the API', async () => {
    const api = createApiMock()
    const wrapper = mount(MgrMovementsView, {
      global: {
        provide: { api },
      },
    })

    await flushView()

    const options = wrapper.findAll('#kind option').map((opt) => ({
      value: opt.attributes('value'),
      text: opt.text(),
    }))

    expect(options).toEqual([
      { value: '', text: 'Todos' },
      { value: 'TICKET_PAYMENT', text: 'Pagamento ticket' },
      { value: 'PACKAGE_PAYMENT', text: 'Pagamento pacote' },
      { value: 'LOJISTA_USAGE', text: 'Uso lojista' },
      { value: 'CLIENT_USAGE', text: 'Uso cliente' },
    ])

    await wrapper.get('#kind').setValue('PACKAGE_PAYMENT')
    await wrapper.get('button.btn-primary').trigger('click')

    expect(api.get).toHaveBeenLastCalledWith('/manager/movements', {
      params: expect.objectContaining({ kind: 'PACKAGE_PAYMENT' }),
    })
  })
})
