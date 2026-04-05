package com.estacionamento.parking.util

import java.time.Instant
import java.time.ZoneId

object DeviceClock {
    private val BR: ZoneId = ZoneId.of("America/Sao_Paulo")
    private const val MAX_SKEW_MS: Long = 5L * 60 * 1000

    /** Data civil `YYYY-MM-DD` em America/Sao_Paulo. */
    fun calendarDateKeyBr(instant: Instant): String {
        val d = instant.atZone(BR).toLocalDate()
        return String.format(java.util.Locale.US, "%04d-%02d-%02d", d.year, d.monthValue, d.dayOfMonth)
    }

    fun isAcceptable(serverUtc: Instant, deviceNow: Instant): Boolean {
        val skew = kotlin.math.abs(serverUtc.toEpochMilli() - deviceNow.toEpochMilli())
        if (skew > MAX_SKEW_MS) return false
        return calendarDateKeyBr(serverUtc) == calendarDateKeyBr(deviceNow)
    }
}

fun siteRootFromApiBase(apiBase: String): String {
    val t = apiBase.trimEnd('/')
    return if (t.endsWith("/api/v1")) t.removeSuffix("/api/v1").trimEnd('/') else t
}
