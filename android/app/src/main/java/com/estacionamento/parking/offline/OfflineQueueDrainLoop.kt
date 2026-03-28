package com.estacionamento.parking.offline

/**
 * Núcleo da drenagem SPEC §10: até 5 tentativas, backoff entre elas.
 * Extraído para testes sem OkHttp/Android.
 */
object OfflineQueueDrainLoop {
    private const val MAX_ATTEMPTS = 5

    suspend fun drainWithBackoff(
        sleepMs: suspend (Long) -> Unit,
        tryOnce: suspend () -> Boolean,
    ): Boolean {
        var success = false
        for (attempt in 0 until MAX_ATTEMPTS) {
            if (attempt > 0) {
                sleepMs(OfflineBackoff.delayMsBeforeAttempt(attempt))
            }
            if (tryOnce()) {
                success = true
                break
            }
        }
        return success
    }
}
