package com.estacionamento.parking.network

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class CardPayOutcomeTest {
    @Test
    fun toOutcome_syncPaid() {
        val r = CardPayResponse(paymentId = "x", status = "PAID", provider = "stub")
        assertTrue(r.toOutcome(false) is CardPayOutcome.SyncPaid)
    }

    @Test
    fun toOutcome_hosted_prefersSandboxWhenFlag() {
        val r = CardPayResponse(
            mode = "hosted_checkout",
            initPoint = "https://prod",
            sandboxInitPoint = "https://sandbox",
        )
        val o = r.toOutcome(true) as CardPayOutcome.HostedCheckout
        assertEquals("https://sandbox", o.openUrl)
    }

    @Test
    fun toOutcome_hosted_initWhenNoSandboxPreference() {
        val r = CardPayResponse(
            mode = "hosted_checkout",
            initPoint = "https://prod",
            sandboxInitPoint = "https://sandbox",
        )
        val o = r.toOutcome(false) as CardPayOutcome.HostedCheckout
        assertEquals("https://prod", o.openUrl)
    }

    @Test
    fun toOutcome_hosted_nullWithoutUrls() {
        val r = CardPayResponse(mode = "hosted_checkout")
        assertNull(r.toOutcome(false))
    }
}
