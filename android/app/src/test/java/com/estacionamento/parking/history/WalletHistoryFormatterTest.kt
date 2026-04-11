package com.estacionamento.parking.history

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class WalletHistoryFormatterTest {
    @Test
    fun kind_labels_spec_5_14() {
        assertEquals("Compra", WalletHistoryFormatter.kindLabel("PURCHASE"))
        assertEquals("Uso", WalletHistoryFormatter.kindLabel("USAGE"))
    }

    @Test
    fun delta_hours_purchase_plus() {
        assertEquals("+5 h", WalletHistoryFormatter.formatDeltaHours("PURCHASE", 5))
        assertEquals("-2 h", WalletHistoryFormatter.formatDeltaHours("USAGE", -2))
    }

    @Test
    fun formats_amount_and_timestamp_like_web() {
        assertEquals("R$ 10,50", WalletHistoryFormatter.formatAmountBrl("10.5"))
        assertTrue(
            WalletHistoryFormatter.formatWhen("2026-04-11T15:30:00Z")
                .matches(Regex("""\d{2}/\d{2}/\d{4} \d{2}:\d{2}""")),
        )
    }
}
