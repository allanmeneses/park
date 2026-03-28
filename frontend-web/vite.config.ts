import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    exclude: ['**/node_modules/**', '**/e2e/**'],
    environment: 'jsdom',
    globals: true,
    env: {
      VITE_API_BASE: 'http://localhost:8080/api/v1',
    },
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: [
        'src/lib/**/*.ts',
        'src/stores/*.ts',
        'src/session/**/*.ts',
        'src/api/**/*.ts',
        'src/router/roleAccess.ts',
        'src/strings.ts',
      ],
      exclude: [
        'src/**/*.spec.ts',
        'src/**/*.test.ts',
        'src/main.ts',
        'src/**/*.d.ts',
      ],
      thresholds: {
        lines: 60,
        functions: 50,
        branches: 50,
        statements: 60,
      },
    },
  },
})
