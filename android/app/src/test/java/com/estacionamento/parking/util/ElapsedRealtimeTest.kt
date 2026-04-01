package com.estacionamento.parking.util

import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.Instant

class ElapsedRealtimeTest {
    @Test
    fun elapsedWholeSeconds_nonNegative() {
        val a = Instant.parse("2026-03-31T10:00:00Z")
        val b = Instant.parse("2026-03-31T10:00:45Z")
        assertEquals(45, elapsedWholeSeconds(a, b))
        assertEquals(0, elapsedWholeSeconds(b, a))
    }

    @Test
    fun formatElapsedPtBr() {
        assertEquals("1 h 1 min 5 s", formatElapsedPtBr(3665))
        assertEquals("0 h 1 min 30 s", formatElapsedPtBr(90))
        assertEquals("0 h 0 min 0 s", formatElapsedPtBr(0))
        assertEquals("0 h 0 min 59 s", formatElapsedPtBr(59))
    }
}
