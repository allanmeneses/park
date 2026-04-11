package com.estacionamento.parking.network

import java.util.Locale

object RechargePackages {
    fun title(pkg: RechargePackageDto): String =
        pkg.displayName.ifBlank { "${pkg.hours} h" }

    fun priceNumber(raw: String): Double =
        raw.replace(',', '.').toDoubleOrNull() ?: 0.0

    fun compare(a: RechargePackageDto, b: RechargePackageDto): Int =
        compareValuesBy(
            a,
            b,
            { it.sortOrder },
            { -it.isPromo.compareTo(false) },
            { priceNumber(it.price) },
            { it.hours },
            { it.displayName.lowercase(Locale.forLanguageTag("pt-BR")) },
        )
}
