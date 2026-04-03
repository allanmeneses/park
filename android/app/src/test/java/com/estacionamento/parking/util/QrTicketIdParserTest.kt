package com.estacionamento.parking.util

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class QrTicketIdParserTest {
    @Test
    fun firstUuid_from_raw() {
        val id = "550e8400-e29b-41d4-a716-446655440000"
        assertEquals(id, QrTicketIdParser.firstUuid(id))
    }

    @Test
    fun firstUuid_from_url() {
        val id = "550e8400-e29b-41d4-a716-446655440000"
        assertEquals(
            id,
            QrTicketIdParser.firstUuid("https://app.example/ticket/$id?utm=x"),
        )
    }

    @Test
    fun firstUuid_blank_null() {
        assertNull(QrTicketIdParser.firstUuid(""))
        assertNull(QrTicketIdParser.firstUuid("   "))
    }
}
