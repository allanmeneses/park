import { describe, expect, it } from 'vitest'
import {
  calendarDateKeyInBrasilia,
  healthUrlFromApiBase,
  isDeviceClockAcceptable,
} from './deviceClockValidation'

describe('healthUrlFromApiBase', () => {
  it('remove /api/v1 e acrescenta /health', () => {
    expect(healthUrlFromApiBase('http://localhost:8080/api/v1')).toBe('http://localhost:8080/health')
    expect(healthUrlFromApiBase('http://localhost:8080/api/v1/')).toBe('http://localhost:8080/health')
  })
})

describe('calendarDateKeyInBrasilia', () => {
  it('produz YYYY-MM-DD no fuso de Brasília', () => {
    const d = new Date('2026-04-03T15:00:00.000Z')
    expect(calendarDateKeyInBrasilia(d)).toBe('2026-04-03')
  })
})

describe('isDeviceClockAcceptable', () => {
  it('aceita relógio alinhado (mesmo instante)', () => {
    const t = '2026-04-03T15:00:00.000Z'
    expect(isDeviceClockAcceptable(t, new Date(t))).toBe(true)
  })

  it('aceita até 5 min de diferença e mesma data em Brasília', () => {
    expect(
      isDeviceClockAcceptable('2026-04-03T15:00:00.000Z', new Date('2026-04-03T15:05:00.000Z')),
    ).toBe(true)
    expect(
      isDeviceClockAcceptable('2026-04-03T15:00:00.000Z', new Date('2026-04-03T14:55:00.000Z')),
    ).toBe(true)
  })

  it('rejeita diferença > 5 minutos', () => {
    expect(
      isDeviceClockAcceptable('2026-04-03T15:00:00.000Z', new Date('2026-04-03T15:06:00.000Z')),
    ).toBe(false)
  })

  it('rejeita data civil diferente em Brasília mesmo com skew pequeno', () => {
    expect(
      isDeviceClockAcceptable('2026-04-03T02:59:00.000Z', new Date('2026-04-03T03:02:00.000Z')),
    ).toBe(false)
  })

  it('ISO inválido não bloqueia (ausência de referência)', () => {
    expect(isDeviceClockAcceptable('not-a-date', new Date())).toBe(true)
  })
})
