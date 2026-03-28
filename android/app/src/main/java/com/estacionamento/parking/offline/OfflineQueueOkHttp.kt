package com.estacionamento.parking.offline

import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.auth.JwtRoleParser
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody

private val jsonMedia = "application/json; charset=utf-8".toMediaType()

suspend fun OfflineQueueItem.postWith(
    client: OkHttpClient,
    rootBaseUrl: String,
    prefs: AuthPrefs,
): Boolean = withContext(Dispatchers.IO) {
    val base = rootBaseUrl.trimEnd('/')
    val p = path.trimStart('/')
    val url = "$base/$p"
    val bodyBytes = (bodyJson ?: "{}").toByteArray(Charsets.UTF_8)
    val reqBuilder = Request.Builder()
        .url(url)
        .post(bodyBytes.toRequestBody(jsonMedia))
        .addHeader("Content-Type", "application/json")
        .addHeader("Idempotency-Key", idempotencyKey)
    prefs.accessToken?.let { reqBuilder.header("Authorization", "Bearer $it") }
    val role = prefs.accessToken?.let { JwtRoleParser.roleFromAccessToken(it) }
    if (role == "SUPER_ADMIN" && !prefs.activeParkingId.isNullOrBlank()) {
        reqBuilder.header("X-Parking-Id", prefs.activeParkingId!!)
    }
    client.newCall(reqBuilder.build()).execute().use { it.isSuccessful }
}
