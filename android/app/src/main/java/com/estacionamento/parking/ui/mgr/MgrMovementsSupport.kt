package com.estacionamento.parking.ui.mgr

import com.estacionamento.parking.network.MovementItemDto
import com.estacionamento.parking.util.parseApiInstant
import java.time.Instant
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter

data class QuickRange(val fromUtc: String, val toUtc: String)

object MgrMovementsSupport {
    private val inputFmt = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm").withZone(ZoneOffset.UTC)
    private val utcFmt = DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss").withZone(ZoneOffset.UTC)

    fun toInputUtc(instant: Instant): String = inputFmt.format(instant)

    fun parseInputUtc(value: String): String? =
        runCatching { Instant.parse("${value}:00Z").toString() }.getOrNull()

    fun quickRange(mode: String, now: Instant = Instant.now()): QuickRange {
        val from = when (mode) {
            "24h" -> now.minusSeconds(24 * 60 * 60L)
            "30d" -> now.minusSeconds(30 * 24 * 60 * 60L)
            else -> now.minusSeconds(7 * 24 * 60 * 60L)
        }
        return QuickRange(fromUtc = toInputUtc(from), toUtc = toInputUtc(now))
    }

    fun formatUtc(raw: String): String =
        parseApiInstant(raw)?.let(utcFmt::format) ?: raw

    fun splitText(row: MovementItemDto): String {
        if (row.kind != "TICKET_PAYMENT") return "—"
        return when (row.ticketSplitType) {
            "MIXED" -> "Misto (lojista ${row.hoursLojista}h, cliente ${row.hoursCliente}h, direto ${row.hoursDirect}h)"
            "LOJISTA_ONLY" -> "Lojista (${row.hoursLojista}h)"
            "CLIENT_WALLET_ONLY" -> "Cliente carteira (${row.hoursCliente}h)"
            else -> "Cliente direto"
        }
    }
}
