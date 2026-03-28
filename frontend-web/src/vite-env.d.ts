/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare module 'qrcode' {
  export function toDataURL(
    text: string,
    options?: { width?: number; margin?: number },
  ): Promise<string>
}
