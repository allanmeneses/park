package com.estacionamento.parking.ui.mgr

import com.estacionamento.parking.network.MovementItemDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class MgrMovementsSupportTest {
    @Test
    fun quick_range_defaults_to_seven_days() {
        val now = Instant.parse("2026-04-11T12:00:00Z")

        val range = MgrMovementsSupport.quickRange("7d", now)

        assertEquals("2026-04-04T12:00", range.fromUtc)
        assertEquals("2026-04-11T12:00", range.toUtc)
    }

    @Test
    fun parses_and_formats_utc_values() {
        assertEquals("2026-04-11T12:34:00Z", MgrMovementsSupport.parseInputUtc("2026-04-11T12:34"))
        assertEquals("2026-04-11 12:34:56", MgrMovementsSupport.formatUtc("2026-04-11T12:34:56Z"))
    }

    @Test
    fun describes_ticket_split_like_web() {
        val text = MgrMovementsSupport.splitText(
            MovementItemDto(
                at = "2026-04-11T12:00:00Z",
                kind = "TICKET_PAYMENT",
                amount = "10.00",
                ref = "abc",
                method = "PIX",
                ticketSplitType = "MIXED",
                hoursLojista = 1,
                hoursCliente = 2,
                hoursDirect = 3,
            ),
        )

        assertTrue(text.contains("Misto"))
        assertTrue(text.contains("lojista 1h"))
        assertTrue(text.contains("cliente 2h"))
    }
}
