package com.estacionamento.parking.ui.mgr

import com.estacionamento.parking.util.formatApiInstantForDeviceLocal

object MgrSettingsSupport {
    fun grantValiditySummary(sameDayOnly: Boolean): String =
        if (sameDayOnly) {
            "Na virada do dia, o saldo bonificado do lojista deixa de valer para uso."
        } else {
            "As bonificações do lojista ficam acumuladas por prazo indeterminado."
        }

    fun auditChangeText(label: String, from: String, to: String): String =
        "$label: de $from para $to"

    fun formatAuditWhen(raw: String): String = formatApiInstantForDeviceLocal(raw)
}
