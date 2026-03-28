package com.estacionamento.parking.offline

import org.junit.Assert.assertEquals
import org.junit.Test

/** SPEC_FRONTEND §10 — espelha backoff da fila Web (1s, 2s, 4s, 8s, 16s). */
class OfflineBackoffTest {

    @Test
    fun delay_before_first_attempt_is_zero() {
        assertEquals(0L, OfflineBackoff.delayMsBeforeAttempt(0))
    }

    @Test
    fun delays_match_spec_between_attempts() {
        assertEquals(1000L, OfflineBackoff.delayMsBeforeAttempt(1))
        assertEquals(2000L, OfflineBackoff.delayMsBeforeAttempt(2))
        assertEquals(4000L, OfflineBackoff.delayMsBeforeAttempt(3))
        assertEquals(8000L, OfflineBackoff.delayMsBeforeAttempt(4))
    }

    @Test
    fun delay_falls_back_to_sixteen_seconds_after_defined_backoff() {
        assertEquals(16000L, OfflineBackoff.delayMsBeforeAttempt(5))
        assertEquals(16000L, OfflineBackoff.delayMsBeforeAttempt(99))
    }
}
