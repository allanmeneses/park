package com.estacionamento.parking.auth

/** SPEC_FRONTEND §3.3 — agendar refresh em expires_in − 120 s. */
object RefreshScheduling {
    private const val MIN_DELAY_SECONDS = 30

    fun delaySecondsUntilRefresh(expiresInSeconds: Int, skewSeconds: Int = 120): Int =
        (expiresInSeconds - skewSeconds).coerceAtLeast(MIN_DELAY_SECONDS)

    fun delayMsUntilRefresh(expiresInSeconds: Int, skewSeconds: Int = 120): Long =
        delaySecondsUntilRefresh(expiresInSeconds, skewSeconds) * 1000L

    /** Próximo refresh em exp − 120 s a partir de agora (retomada do app). */
    fun delayMsFromAbsoluteExpiry(accessExpiresAtEpochSec: Long, nowEpochSec: Long): Long {
        val delaySec = (accessExpiresAtEpochSec - 120 - nowEpochSec).toInt().coerceAtLeast(MIN_DELAY_SECONDS)
        return delaySec * 1000L
    }
}
