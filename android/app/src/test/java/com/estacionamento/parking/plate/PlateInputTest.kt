package com.estacionamento.parking.plate

import org.junit.Assert.assertEquals
import org.junit.Test

class PlateInputTest {
    @Test
    fun sanitizePrefixLettersThenTail() {
        assertEquals("AB", PlateInput.sanitize("ab*12"))
        assertEquals("ABC12", PlateInput.sanitize("abc*12"))
        assertEquals("ABC1234", PlateInput.sanitize("abc-1234"))
        assertEquals("ABC1D23", PlateInput.sanitize("abc1d23"))
        assertEquals("ABC1234", PlateInput.sanitize("1abc1234"))
        assertEquals("ABC1234", PlateInput.sanitize("abcd1234"))
    }
}
