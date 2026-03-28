package com.estacionamento.parking.auth

import org.junit.Assert.assertEquals
import org.junit.Test

/** SPEC_FRONTEND §3.3 — timer em expires_in - 120 s. */
class RefreshSchedulingTest {

    @Test
    fun delay_seconds_is_expires_minus_skew() {
        assertEquals(28680, RefreshScheduling.delaySecondsUntilRefresh(28800, skewSeconds = 120))
    }

    @Test
    fun minimum_delay_floor_prevents_immediate_timer() {
        assertEquals(30, RefreshScheduling.delaySecondsUntilRefresh(60, skewSeconds = 120))
        assertEquals(30, RefreshScheduling.delaySecondsUntilRefresh(100, skewSeconds = 120))
    }

    @Test
    fun delay_milliseconds_for_coroutine() {
        assertEquals(30_000L, RefreshScheduling.delayMsUntilRefresh(60, skewSeconds = 120))
        assertEquals(28680_000L, RefreshScheduling.delayMsUntilRefresh(28800, skewSeconds = 120))
    }

    @Test
    fun delay_from_absolute_expiry_matches_exp_minus_skew_minus_now() {
        val nowSec = 1_000_000L
        val expSec = nowSec + 28800
        assertEquals(28680_000L, RefreshScheduling.delayMsFromAbsoluteExpiry(expSec, nowSec))
    }

    @Test
    fun absolute_expiry_uses_minimum_when_almost_expired() {
        val nowSec = 100L
        val expSec = nowSec + 60
        assertEquals(30_000L, RefreshScheduling.delayMsFromAbsoluteExpiry(expSec, nowSec))
    }
}
