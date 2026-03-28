package com.estacionamento.parking.util

import java.time.Instant
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

/** SPEC_FRONTEND §5.2 — exibir instante API em `dd/MM/yyyy HH:mm` no fuso do dispositivo. */
fun formatApiInstantForDeviceLocal(iso: String): String {
    if (iso.isBlank()) return iso
    val instant = try {
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
                return iso
            }
        }
    }
    val fmt = DateTimeFormatter.ofPattern("dd/MM/yyyy HH:mm", Locale("pt", "BR"))
        .withZone(ZoneId.systemDefault())
    return fmt.format(instant)
}
