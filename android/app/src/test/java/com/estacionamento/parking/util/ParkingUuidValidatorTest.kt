package com.estacionamento.parking.util

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ParkingUuidValidatorTest {
    @Test
    fun accepts_uuid_v4_case_insensitive() {
        assertTrue(ParkingUuidValidator.isValid("550E8400-E29B-41D4-A716-446655440000"))
    }

    @Test
    fun rejects_non_v4_uuid() {
        assertFalse(ParkingUuidValidator.isValid("550e8400-e29b-11d4-a716-446655440000"))
    }

    @Test
    fun rejects_blank_or_malformed_values() {
        assertFalse(ParkingUuidValidator.isValid(""))
        assertFalse(ParkingUuidValidator.isValid("abc"))
    }
}
