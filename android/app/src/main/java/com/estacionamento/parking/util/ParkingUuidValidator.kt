package com.estacionamento.parking.util

object ParkingUuidValidator {
    private val uuidV4 =
        Regex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOption.IGNORE_CASE)

    fun isValid(raw: String): Boolean = uuidV4.matches(raw.trim())
}
