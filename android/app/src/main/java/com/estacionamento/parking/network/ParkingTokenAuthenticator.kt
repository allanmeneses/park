package com.estacionamento.parking.network

import com.estacionamento.parking.auth.AuthPrefs
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.runBlocking
import okhttp3.Authenticator
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route

/** SPEC_FRONTEND §3.3 — uma tentativa de refresh em 401 (exceto login/refresh). */
class ParkingTokenAuthenticator(
    private val prefs: AuthPrefs,
    private val authRefresh: ParkingAuthRefresh,
) : Authenticator {

    private val lock = Any()

    override fun authenticate(route: Route?, response: Response): Request? {
        val path = response.request.url.encodedPath
        if (path.contains("auth/login", ignoreCase = true)) return null
        if (path.contains("auth/refresh", ignoreCase = true)) return null
        synchronized(lock) {
            if (responseCount(response) >= 2) return null
            val rt = prefs.refreshToken ?: return null
            val access = try {
                runBlocking(Dispatchers.IO) {
                    val r = authRefresh.refresh(RefreshBody(rt))
                    prefs.accessToken = r.accessToken
                    prefs.refreshToken = r.refreshToken
                    val now = System.currentTimeMillis() / 1000
                    prefs.accessTokenExpiresAtEpochSec = now + r.expiresIn
                    r.accessToken
                }
            } catch (_: Exception) {
                return null
            }
            return response.request.newBuilder()
                .header("Authorization", "Bearer $access")
                .build()
        }
    }

    private fun responseCount(response: Response): Int {
        var c = 1
        var p = response.priorResponse
        while (p != null) {
            c++
            p = p.priorResponse
        }
        return c
    }
}
