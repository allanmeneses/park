package com.estacionamento.parking.auth

import com.estacionamento.parking.network.LoginResponse
import com.estacionamento.parking.network.ParkingAuthRefresh
import com.estacionamento.parking.network.RefreshBody
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/** SPEC_FRONTEND §3.3 — refresh proativo (expires_in − 120 s), em loop no mesmo Job. */
class TokenRefreshCoordinator(
    private val scope: CoroutineScope,
    private val prefs: AuthPrefs,
    private val authRefresh: ParkingAuthRefresh,
    private val onTokensRefreshed: () -> Unit = {},
) {
    private var job: Job? = null

    fun scheduleAfterLoginOrRefresh(expiresInSeconds: Int) {
        val now = System.currentTimeMillis() / 1000
        prefs.accessTokenExpiresAtEpochSec = now + expiresInSeconds
        startLoop(RefreshScheduling.delayMsUntilRefresh(expiresInSeconds))
    }

    fun scheduleResumeFromStoredExpiry() {
        val exp = prefs.accessTokenExpiresAtEpochSec ?: return
        val now = System.currentTimeMillis() / 1000
        startLoop(RefreshScheduling.delayMsFromAbsoluteExpiry(exp, now))
    }

    private fun startLoop(initialDelayMs: Long) {
        job?.cancel()
        job = scope.launch {
            delay(initialDelayMs)
            while (true) {
                val rt = prefs.refreshToken ?: break
                val r = runCatching { authRefresh.refresh(RefreshBody(rt)) }.getOrNull() ?: break
                applyRefreshResponse(r)
                delay(RefreshScheduling.delayMsUntilRefresh(r.expiresIn))
            }
        }
    }

    private fun applyRefreshResponse(r: LoginResponse) {
        prefs.accessToken = r.accessToken
        prefs.refreshToken = r.refreshToken
        val nowSec = System.currentTimeMillis() / 1000
        prefs.accessTokenExpiresAtEpochSec = nowSec + r.expiresIn
        onTokensRefreshed()
    }

    fun cancel() {
        job?.cancel()
        job = null
    }
}
