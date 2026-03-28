package com.estacionamento.parking.plate

object PlateValidator {
    private val mercosul = Regex("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$")
    private val legado = Regex("^[A-Z]{3}[0-9]{4}$")

    fun normalize(raw: String): String =
        raw.replace(Regex("[\\s-]"), "").uppercase()

    fun isValidNormalized(plate: String): Boolean =
        mercosul.matches(plate) || legado.matches(plate)

    fun isValid(raw: String): Boolean = isValidNormalized(normalize(raw))
}
