package com.estacionamento.parking.offline

/** SPEC_FRONTEND §10 — mesma sequência que `frontend-web` (offlineQueue.ts). */
object OfflineBackoff {
    private val DELAYS_MS = longArrayOf(1000, 2000, 4000, 8000, 16000)

    /**
     * @param zeroBasedAttempt índice da tentativa (0 = primeira, sem espera prévia).
     */
    fun delayMsBeforeAttempt(zeroBasedAttempt: Int): Long {
        if (zeroBasedAttempt <= 0) return 0L
        val idx = zeroBasedAttempt - 1
        return if (idx < DELAYS_MS.size) DELAYS_MS[idx] else 16000L
    }
}
