package com.estacionamento.parking.util

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class DeviceClockTest {
    @Test
    fun calendarDateKeyBr_para_instante_utc() {
        val i = Instant.parse("2026-04-03T15:00:00Z")
        assertEquals("2026-04-03", DeviceClock.calendarDateKeyBr(i))
    }

    @Test
    fun aceita_mesmo_instante() {
        val t = Instant.parse("2026-04-03T15:00:00Z")
        assertTrue(DeviceClock.isAcceptable(t, t))
    }

    @Test
    fun aceita_ate_5_min() {
        assertTrue(
            DeviceClock.isAcceptable(
                Instant.parse("2026-04-03T15:00:00Z"),
                Instant.parse("2026-04-03T15:05:00Z"),
            ),
        )
    }

    @Test
    fun rejeita_mais_de_5_min() {
        assertFalse(
            DeviceClock.isAcceptable(
                Instant.parse("2026-04-03T15:00:00Z"),
                Instant.parse("2026-04-03T15:06:00Z"),
            ),
        )
    }

    @Test
    fun rejeita_data_civil_diferente_em_brasilia() {
        assertFalse(
            DeviceClock.isAcceptable(
                Instant.parse("2026-04-03T02:59:00Z"),
                Instant.parse("2026-04-03T03:02:00Z"),
            ),
        )
    }

    @Test
    fun siteRootFromApiBase_remove_api_v1() {
        assertEquals("http://10.0.2.2:8080", siteRootFromApiBase("http://10.0.2.2:8080/api/v1"))
    }
}
