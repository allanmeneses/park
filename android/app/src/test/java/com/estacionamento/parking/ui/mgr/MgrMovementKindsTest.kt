package com.estacionamento.parking.ui.mgr

import org.junit.Assert.assertEquals
import org.junit.Test

class MgrMovementKindsTest {
    @Test
    fun matches_web_kind_options() {
        assertEquals(
            listOf(
                "" to "Todos",
                "TICKET_PAYMENT" to "Pagamento ticket",
                "PACKAGE_PAYMENT" to "Pagamento pacote",
                "LOJISTA_USAGE" to "Uso lojista",
                "CLIENT_USAGE" to "Uso cliente",
            ),
            MgrMovementKinds.options.map { it.value to it.label },
        )
    }
}
