package com.estacionamento.parking.ui

import org.junit.Assert.assertEquals
import org.junit.Test

class UiStringsGrantResumoTest {
    @Test
    fun grantSaldoBonificadoResumo_formats_hours() {
        assertEquals(
            "Saldo bonificado da placa: 1 h. Seu saldo: 13 h.",
            UiStrings.grantSaldoBonificadoResumo(1, 13),
        )
    }
}
