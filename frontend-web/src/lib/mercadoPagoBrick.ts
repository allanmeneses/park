export interface CardPaymentBrickFormData {
  token?: string
  payment_method_id?: string
  issuer_id?: string | number
  installments?: string | number
  payer?: {
    email?: string
    identification?: {
      type?: string
      number?: string
    }
  }
}

export interface CardPaymentBrickController {
  unmount(): void
}

interface CardPaymentBrickSettings {
  initialization: {
    amount: number
  }
  callbacks: {
    onReady?: () => void
    onSubmit: (formData: CardPaymentBrickFormData) => Promise<void>
    onError?: (error: unknown) => void
  }
}

interface MercadoPagoBricksBuilder {
  create(
    brickName: 'cardPayment',
    containerId: string,
    settings: CardPaymentBrickSettings,
  ): Promise<CardPaymentBrickController>
}

interface MercadoPagoInstance {
  bricks(): MercadoPagoBricksBuilder
}

export interface MercadoPagoConstructor {
  new (publicKey: string, options?: { locale?: string }): MercadoPagoInstance
}

declare global {
  interface Window {
    MercadoPago?: MercadoPagoConstructor
  }
}

let sdkPromise: Promise<MercadoPagoConstructor> | null = null

export function loadMercadoPagoSdk(): Promise<MercadoPagoConstructor> {
  if (window.MercadoPago) return Promise.resolve(window.MercadoPago)
  if (sdkPromise) return sdkPromise

  sdkPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector<HTMLScriptElement>('script[data-mp-sdk="true"]')
    if (existing) {
      existing.addEventListener('load', handleLoad, { once: true })
      existing.addEventListener('error', handleError, { once: true })
      return
    }

    const script = document.createElement('script')
    script.src = 'https://sdk.mercadopago.com/js/v2'
    script.async = true
    script.dataset.mpSdk = 'true'
    script.addEventListener('load', handleLoad, { once: true })
    script.addEventListener('error', handleError, { once: true })
    document.head.appendChild(script)

    function handleLoad(): void {
      if (window.MercadoPago) resolve(window.MercadoPago)
      else {
        sdkPromise = null
        reject(new Error('Mercado Pago SDK indisponível após carregar o script.'))
      }
    }

    function handleError(): void {
      sdkPromise = null
      reject(new Error('Falha ao carregar o SDK do Mercado Pago.'))
    }
  })

  return sdkPromise
}
