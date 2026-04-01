package com.estacionamento.parking.ui.mgr

import java.util.Locale

object MgrInsightsFormatter {
    fun moneyBrl(raw: String): String {
        val n = raw.replace(',', '.').toDoubleOrNull() ?: 0.0
        return String.format(Locale.forLanguageTag("pt-BR"), "R$ %.2f", n)
    }

    fun hourLabel(hour: Int): String = "%02d:00".format(hour.coerceIn(0, 23))
}
