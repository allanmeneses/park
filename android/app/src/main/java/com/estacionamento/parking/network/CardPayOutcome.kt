package com.estacionamento.parking.network

/** Resultado interpretado de [CardPayResponse] (SPEC_FRONTEND §5.8). */
sealed class CardPayOutcome {
    data object SyncPaid : CardPayOutcome()

    data class HostedCheckout(val openUrl: String) : CardPayOutcome()

    data class EmbeddedBricks(val provider: String?, val publicKey: String?) : CardPayOutcome()

    data class Pending(val providerStatus: String?, val providerStatusDetail: String?) : CardPayOutcome()

    data class Failed(val status: String, val message: String) : CardPayOutcome()
}

fun CardPayResponse.toOutcome(preferSandboxInitPoint: Boolean): CardPayOutcome? {
    if (mode.equals("embedded_bricks", ignoreCase = true)) {
        return CardPayOutcome.EmbeddedBricks(provider = provider, publicKey = publicKey)
    }
    if (mode.equals("hosted_checkout", ignoreCase = true)) {
        val url = when {
            preferSandboxInitPoint && !sandboxInitPoint.isNullOrBlank() -> sandboxInitPoint
            !initPoint.isNullOrBlank() -> initPoint
            else -> sandboxInitPoint
        } ?: return null
        return CardPayOutcome.HostedCheckout(url)
    }
    if (status.equals("PAID", ignoreCase = true)) return CardPayOutcome.SyncPaid
    if (status.equals("PENDING", ignoreCase = true)) {
        return CardPayOutcome.Pending(providerStatus = providerStatus, providerStatusDetail = providerStatusDetail)
    }
    if (status.equals("FAILED", ignoreCase = true) || status.equals("EXPIRED", ignoreCase = true)) {
        val msg = providerStatusDetail ?: providerStatus ?: status ?: "FAILED"
        return CardPayOutcome.Failed(status = status ?: "FAILED", message = msg)
    }
    return null
}
