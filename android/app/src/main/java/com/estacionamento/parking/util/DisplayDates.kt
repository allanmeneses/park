package com.estacionamento.parking.util

import java.time.Instant
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** Mesma tolerância de formato que a UI (Instant, OffsetDateTime, local). */
fun parseApiInstant(iso: String): Instant? {
    if (iso.isBlank()) return null
    return try {
        Instant.parse(iso)
    } catch (_: Exception) {
        try {
            OffsetDateTime.parse(iso).toInstant()
        } catch (_: Exception) {
            try {
                LocalDateTime.parse(iso, DateTimeFormatter.ISO_LOCAL_DATE_TIME)
                    .atZone(ZoneId.systemDefault())
                    .toInstant()
            } catch (_: Exception) {
                null
            }
        }
    }
}

/** Exibir instante API em `dd/MM/yyyy HH:mm` no fuso de Brasília (America/Sao_Paulo), alinhado à Web e ao manual. */
fun formatApiInstantForDeviceLocal(iso: String): String {
    if (iso.isBlank()) return iso
    val instant = parseApiInstant(iso) ?: return iso
    val fmt = DateTimeFormatter.ofPattern("dd/MM/yyyy HH:mm", Locale("pt", "BR"))
        .withZone(ZoneId.of("America/Sao_Paulo"))
    return fmt.format(instant)
}
