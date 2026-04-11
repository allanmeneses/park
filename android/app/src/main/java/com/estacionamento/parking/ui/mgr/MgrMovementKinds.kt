package com.estacionamento.parking.ui.mgr

data class MgrMovementKindOption(val value: String, val label: String)

object MgrMovementKinds {
    val options =
        listOf(
            MgrMovementKindOption("", "Todos"),
            MgrMovementKindOption("TICKET_PAYMENT", "Pagamento ticket"),
            MgrMovementKindOption("PACKAGE_PAYMENT", "Pagamento pacote"),
            MgrMovementKindOption("LOJISTA_USAGE", "Uso lojista"),
            MgrMovementKindOption("CLIENT_USAGE", "Uso cliente"),
        )

    fun labelFor(value: String): String =
        options.firstOrNull { it.value == value }?.label ?: options.first().label
}
