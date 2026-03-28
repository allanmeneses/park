package com.estacionamento.parking.util

import org.junit.Assert.assertTrue
import org.junit.Test

class DisplayDatesTest {
    @Test
    fun formats_z_instant() {
        val s = formatApiInstantForDeviceLocal("2024-06-15T14:30:00Z")
        assertTrue(s.matches(Regex("""\d{2}/\d{2}/\d{4} \d{2}:\d{2}""")))
    }
}
