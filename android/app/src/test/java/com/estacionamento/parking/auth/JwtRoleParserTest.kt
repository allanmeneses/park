package com.estacionamento.parking.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.util.Base64

class JwtRoleParserTest {
    @Test
    fun reads_ms_claim_role() {
        val payload = """{"sub":"a","http://schemas.microsoft.com/ws/2008/06/identity/claims/role":"MANAGER"}"""
        val enc = encodeJwtSegment(payload)
        val token = "x.$enc.y"
        assertEquals("MANAGER", JwtRoleParser.roleFromAccessToken(token))
    }

    @Test
    fun reads_short_role_key() {
        val payload = """{"sub":"a","role":"CLIENT"}"""
        val enc = encodeJwtSegment(payload)
        assertEquals("CLIENT", JwtRoleParser.roleFromAccessToken("h.$enc.s"))
    }

    private fun encodeJwtSegment(json: String): String =
        Base64.getUrlEncoder().withoutPadding().encodeToString(json.toByteArray(Charsets.UTF_8))

    @Test
    fun invalid_token_returns_null() {
        assertNull(JwtRoleParser.roleFromAccessToken(""))
        assertNull(JwtRoleParser.roleFromAccessToken("onlyone"))
    }
}
