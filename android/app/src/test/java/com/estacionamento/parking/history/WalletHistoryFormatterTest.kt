package com.estacionamento.parking.history

import org.junit.Assert.assertEquals
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
}
