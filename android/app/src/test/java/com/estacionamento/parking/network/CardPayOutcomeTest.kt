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

    @Test
    fun toOutcome_embeddedBricks() {
        val r = CardPayResponse(mode = "embedded_bricks", provider = "mercadopago", publicKey = "pk_test")
        val o = r.toOutcome(false) as CardPayOutcome.EmbeddedBricks
        assertEquals("mercadopago", o.provider)
        assertEquals("pk_test", o.publicKey)
    }

    @Test
    fun toOutcome_pendingAndFailed() {
        val pending = CardPayResponse(status = "PENDING", providerStatus = "in_process", providerStatusDetail = "pending_review_manual")
        val pendingOutcome = pending.toOutcome(false) as CardPayOutcome.Pending
        assertEquals("in_process", pendingOutcome.providerStatus)
        assertEquals("pending_review_manual", pendingOutcome.providerStatusDetail)

        val failed = CardPayResponse(status = "FAILED", providerStatus = "rejected", providerStatusDetail = "cc_rejected_bad_filled_card_number")
        val failedOutcome = failed.toOutcome(false) as CardPayOutcome.Failed
        assertEquals("FAILED", failedOutcome.status)
        assertEquals("cc_rejected_bad_filled_card_number", failedOutcome.message)
    }
}
