package com.estacionamento.parking.util

/** Extrai o primeiro UUID do texto do QR (URL ou ID bruto). */
object QrTicketIdParser {
    private val uuidRegex =
        Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")

    fun firstUuid(text: String): String? = uuidRegex.find(text.trim())?.value?.lowercase()
}
