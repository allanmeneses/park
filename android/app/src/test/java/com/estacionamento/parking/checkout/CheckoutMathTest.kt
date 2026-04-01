package com.estacionamento.parking.checkout

import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.Instant

class CheckoutMathTest {
    @Test
    fun partialHourCeils() {
        val e = Instant.parse("2026-03-31T10:00:00Z")
        val x = Instant.parse("2026-03-31T10:01:00Z")
        assertEquals(1, computeBillableHours(e, x))
    }

    @Test
    fun zeroWhenExitNotAfterEntry() {
        val e = Instant.parse("2026-03-31T12:00:00Z")
        assertEquals(0, computeBillableHours(e, e))
        assertEquals(0, computeBillableHours(e, Instant.parse("2026-03-31T11:00:00Z")))
    }
}
