package com.estacionamento.parking.checkout

import java.time.Instant
import java.time.temporal.ChronoUnit
import kotlin.math.ceil

/** Espelha Parking.Application.Checkout.CheckoutMath. */
fun computeBillableHours(entry: Instant, exit: Instant): Int {
    val sec = ChronoUnit.SECONDS.between(entry, exit)
    if (sec <= 0) return 0
    return ceil(sec / 3600.0).toInt()
}
