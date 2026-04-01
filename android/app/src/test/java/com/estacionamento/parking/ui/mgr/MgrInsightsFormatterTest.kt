package com.estacionamento.parking.ui.mgr

import org.junit.Assert.assertEquals
import org.junit.Test

class MgrInsightsFormatterTest {
    @Test
    fun hourLabel_pads_with_zero() {
        assertEquals("00:00", MgrInsightsFormatter.hourLabel(0))
        assertEquals("09:00", MgrInsightsFormatter.hourLabel(9))
        assertEquals("23:00", MgrInsightsFormatter.hourLabel(23))
    }

    @Test
    fun moneyBrl_formats_decimal() {
        assertEquals("R$ 10,50", MgrInsightsFormatter.moneyBrl("10.5"))
        assertEquals("R$ 0,00", MgrInsightsFormatter.moneyBrl("0"))
    }
}
