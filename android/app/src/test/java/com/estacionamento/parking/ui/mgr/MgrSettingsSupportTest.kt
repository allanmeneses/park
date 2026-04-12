package com.estacionamento.parking.ui.mgr

import org.junit.Assert.assertEquals
import org.junit.Test

class MgrSettingsSupportTest {
    @Test
    fun grant_validity_summary_matches_web_copy() {
        assertEquals(
            "Na virada do dia, o saldo bonificado do lojista deixa de valer para uso.",
            MgrSettingsSupport.grantValiditySummary(true),
        )
        assertEquals(
            "As bonificações do lojista ficam acumuladas por prazo indeterminado.",
            MgrSettingsSupport.grantValiditySummary(false),
        )
    }

    @Test
    fun audit_change_text_shows_from_and_to_values() {
        val text =
            MgrSettingsSupport.auditChangeText(
                label = "Validade da bonificação do lojista",
                from = "Prazo indeterminado",
                to = "Somente no dia da bonificação",
            )

        assertEquals(
            "Validade da bonificação do lojista: de Prazo indeterminado para Somente no dia da bonificação",
            text,
        )
    }
}
