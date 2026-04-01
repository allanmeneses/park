package com.estacionamento.parking.util

import java.time.Instant
import java.time.temporal.ChronoUnit
import kotlin.math.max

fun elapsedWholeSeconds(entry: Instant, end: Instant): Long =
    max(0, ChronoUnit.SECONDS.between(entry, end))

/** Texto pt-BR — sempre horas, minutos e segundos (contador ao vivo). */
fun formatElapsedPtBr(totalSeconds: Long): String {
    val s = max(0, totalSeconds)
    val h = s / 3600
    val m = (s % 3600) / 60
    val sec = s % 60
    return "${h} h ${m} min ${sec} s"
}
