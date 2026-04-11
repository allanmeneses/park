package com.estacionamento.parking.history

import com.estacionamento.parking.util.formatApiInstantForDeviceLocal
import java.util.Locale

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

    fun formatWhen(iso: String): String = formatApiInstantForDeviceLocal(iso)

    fun formatAmountBrl(amountStr: String): String {
        val n = amountStr.replace(',', '.').toDoubleOrNull() ?: return amountStr
        return String.format(Locale.forLanguageTag("pt-BR"), "R$ %.2f", n)
    }
}
