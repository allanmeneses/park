package com.estacionamento.parking.plate

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlateValidatorTest {
    @Test
    fun mercosul() {
        assertTrue(PlateValidator.isValid("ABC1D23"))
    }

    @Test
    fun legado() {
        assertTrue(PlateValidator.isValid("ABC1234"))
    }

    @Test
    fun invalid() {
        assertFalse(PlateValidator.isValid("AB1"))
    }

    @Test
    fun normalize() {
        assertTrue(PlateValidator.isValid(" abc-1d23 "))
    }
}
