package com.estacionamento.parking.network

/** Resultado interpretado de [CardPayResponse] (SPEC_FRONTEND §5.8). */
sealed class CardPayOutcome {
    data object SyncPaid : CardPayOutcome()

    data class HostedCheckout(val openUrl: String) : CardPayOutcome()
}

fun CardPayResponse.toOutcome(preferSandboxInitPoint: Boolean): CardPayOutcome? {
    if (mode.equals("hosted_checkout", ignoreCase = true)) {
        val url = when {
            preferSandboxInitPoint && !sandboxInitPoint.isNullOrBlank() -> sandboxInitPoint
            !initPoint.isNullOrBlank() -> initPoint
            else -> sandboxInitPoint
        } ?: return null
        return CardPayOutcome.HostedCheckout(url)
    }
    if (status.equals("PAID", ignoreCase = true)) return CardPayOutcome.SyncPaid
    return null
}
