package com.estacionamento.parking.offline

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class OfflineQueueDrainLoopTest {

    @Test
    fun succeeds_without_sleep_when_first_try_ok() = runTest {
        val sleeps = mutableListOf<Long>()
        var calls = 0
        val ok = OfflineQueueDrainLoop.drainWithBackoff(
            sleepMs = { sleeps.add(it) },
            tryOnce = {
                calls++
                true
            },
        )
        assertTrue(ok)
        assertEquals(1, calls)
        assertTrue(sleeps.isEmpty())
    }

    @Test
    fun uses_backoff_sequence_before_retries() = runTest {
        val sleeps = mutableListOf<Long>()
        var calls = 0
        val ok = OfflineQueueDrainLoop.drainWithBackoff(
            sleepMs = { sleeps.add(it) },
            tryOnce = {
                calls++
                calls >= 3
            },
        )
        assertTrue(ok)
        assertEquals(3, calls)
        assertEquals(listOf(1000L, 2000L), sleeps)
    }

    @Test
    fun fails_after_five_attempts_and_applies_four_delays() = runTest {
        val sleeps = mutableListOf<Long>()
        var calls = 0
        val ok = OfflineQueueDrainLoop.drainWithBackoff(
            sleepMs = { sleeps.add(it) },
            tryOnce = {
                calls++
                false
            },
        )
        assertFalse(ok)
        assertEquals(5, calls)
        assertEquals(listOf(1000L, 2000L, 4000L, 8000L), sleeps)
    }
}
