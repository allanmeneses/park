package com.estacionamento.parking.offline

import com.estacionamento.parking.auth.AuthPrefs
import kotlinx.coroutines.delay
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import okhttp3.OkHttpClient

/** SPEC_FRONTEND §10 — drenagem FIFO com backoff; falha final mantém o item. */
class OfflineQueueDrainer(
    private val store: OfflineQueueStore,
    private val client: OkHttpClient,
    private val rootBaseUrl: String,
    private val prefs: AuthPrefs,
) {
    private val mutex = Mutex()

    suspend fun drainAll(
        sleepMs: suspend (Long) -> Unit = { delay(it) },
        onPermanentFailure: () -> Unit,
    ) = mutex.withLock {
        while (true) {
            val head = store.peekOrNull() ?: break
            var attempts = head.attempts
            val ok = OfflineQueueDrainLoop.drainWithBackoff(
                sleepMs = sleepMs,
                tryOnce = {
                    val item = head.copy(attempts = attempts)
                    val success = item.postWith(client, rootBaseUrl, prefs)
                    if (!success) {
                        attempts++
                        store.updateHeadAttempts(attempts)
                    }
                    success
                },
            )
            if (!ok) {
                onPermanentFailure()
                return
            }
            store.removeHead()
        }
    }
}
