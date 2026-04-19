package com.estacionamento.parking.auth

import java.util.Base64

object JwtRoleParser {
    private val msRoleKey = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"

    fun roleFromAccessToken(token: String): String? {
        val json = decodeJwtPayload(token) ?: return null
        return extractJsonString(json, msRoleKey)
            ?: extractJsonString(json, "role")
            ?: extractJsonString(json, "Role")
    }

    /** Claim `parking_id` no access token (utilizadores do tenant). */
    fun parkingIdFromAccessToken(token: String): String? {
        val json = decodeJwtPayload(token) ?: return null
        return extractJsonString(json, "parking_id")
    }

    /** Claim `exp` (segundos Unix UTC) no access token — fallback quando `access_exp_epoch_sec` não foi persistido. */
    fun accessExpiresAtEpochSecFromAccessToken(token: String): Long? {
        val json = decodeJwtPayload(token) ?: return null
        val m = """"exp"\s*:\s*(\d+)""".toRegex().find(json) ?: return null
        return m.groupValues[1].toLongOrNull()
    }

    private fun decodeJwtPayload(token: String): String? {
        val parts = token.split('.')
        if (parts.size < 2) return null
        return decodePayload(parts[1])
    }

    private fun decodePayload(segment: String): String? = try {
        var s = segment.replace('-', '+').replace('_', '/')
        while (s.length % 4 != 0) s += "="
        String(Base64.getDecoder().decode(s), Charsets.UTF_8)
    } catch (_: Exception) {
        null
    }

    private fun extractJsonString(json: String, key: String): String? {
        val escaped = Regex.escape(key)
        val re = """"$escaped"\s*:\s*"([^"]*)"""".toRegex()
        return re.find(json)?.groupValues?.get(1)?.takeIf { it.isNotEmpty() }
    }
}
