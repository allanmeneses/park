package com.estacionamento.parking.history

/** SPEC_FRONTEND §5.14 / §5.17 — mesma lógica da Web. */
object WalletHistoryFormatter {
    fun kindLabel(kind: String): String = when (kind) {
        "PURCHASE" -> "Compra"
        "USAGE" -> "Uso"
        else -> kind
    }

    fun formatDeltaHours(kind: String, deltaHours: Int): String =
        when (kind) {
            "PURCHASE" -> "+$deltaHours h"
            else -> when {
                deltaHours < 0 -> "$deltaHours h"
                deltaHours > 0 -> "-$deltaHours h"
                else -> "0 h"
            }
        }
}
