package com.estacionamento.parking.util

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.time.Instant

data class HealthResponseJson(val ok: Boolean = false, val serverTimeUtc: String? = null)

suspend fun fetchServerTimeUtcIso(http: OkHttpClient, apiBase: String): String? = withContext(Dispatchers.IO) {
    val url = "${siteRootFromApiBase(apiBase)}/health"
    val req = Request.Builder().url(url).get().build()
    http.newCall(req).execute().use { resp ->
        if (!resp.isSuccessful) return@withContext null
        val body = resp.body?.string() ?: return@withContext null
        val moshi = Moshi.Builder().add(KotlinJsonAdapterFactory()).build()
        val parsed = moshi.adapter(HealthResponseJson::class.java).fromJson(body) ?: return@withContext null
        parsed.serverTimeUtc?.takeIf { it.isNotBlank() }
    }
}

suspend fun shouldBlockAppForClock(http: OkHttpClient, apiBase: String): Boolean {
    val iso = fetchServerTimeUtcIso(http, apiBase) ?: return false
    val server = runCatching { Instant.parse(iso) }.getOrNull() ?: return false
    return !DeviceClock.isAcceptable(server, Instant.now())
}
