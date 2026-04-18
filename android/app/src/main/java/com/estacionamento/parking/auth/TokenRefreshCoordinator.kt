package com.estacionamento.parking.auth

import com.estacionamento.parking.network.LoginResponse
import com.estacionamento.parking.network.ParkingAuthRefresh
import com.estacionamento.parking.network.RefreshBody
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import retrofit2.HttpException

/** SPEC_FRONTEND §3.3 — refresh proativo (expires_in − 120 s), em loop no mesmo Job. */
class TokenRefreshCoordinator(
    private val scope: CoroutineScope,
    private val prefs: AuthPrefs,
    private val authRefresh: ParkingAuthRefresh,
    private val onTokensRefreshed: () -> Unit = {},
    private val onSessionExpired: () -> Unit = {},
) {
    private var job: Job? = null

    fun scheduleAfterLoginOrRefresh(expiresInSeconds: Int) {
        val now = System.currentTimeMillis() / 1000
        prefs.accessTokenExpiresAtEpochSec = now + expiresInSeconds
        startLoop(RefreshScheduling.delayMsUntilRefresh(expiresInSeconds))
    }

    fun scheduleResumeFromStoredExpiry() {
        val stored = prefs.accessTokenExpiresAtEpochSec
        val fromJwt = prefs.accessToken?.let { JwtRoleParser.accessExpiresAtEpochSecFromAccessToken(it) }
        val exp = when {
            stored != null && fromJwt != null -> minOf(stored, fromJwt)
            stored != null -> stored
            fromJwt != null -> fromJwt
            else -> {
                if (!prefs.refreshToken.isNullOrBlank()) {
                    startLoop(0L)
                }
                return
            }
        }
        val now = System.currentTimeMillis() / 1000
        val initial = if (now >= exp - 120) 0L else RefreshScheduling.delayMsFromAbsoluteExpiry(exp, now)
        startLoop(initial)
    }

    private fun startLoop(initialDelayMs: Long) {
        job?.cancel()
        job = scope.launch {
            delay(initialDelayMs.coerceAtLeast(0L))
            while (true) {
                val rt = prefs.refreshToken
                if (rt.isNullOrBlank()) break
                val r = try {
                    authRefresh.refresh(RefreshBody(rt))
                } catch (e: CancellationException) {
                    throw e
                } catch (e: HttpException) {
                    if (e.code() == 401) {
                        prefs.clear()
                        onSessionExpired()
                        break
                    }
                    delay(60_000L)
                    continue
                } catch (_: Exception) {
                    delay(60_000L)
                    continue
                }
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
