package com.estacionamento.parking.errors

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class ApiErrorMapperTest {
    @Test
    fun maps_known_codes_spec_section_8() {
        assertEquals("Formato de placa inválido.", ApiErrorMapper.messageForCode("PLATE_INVALID"))
        assertEquals("Relógio do aparelho incorreto. Ajuste a data e hora.", ApiErrorMapper.messageForCode("CLOCK_SKEW"))
        assertEquals("Muitas tentativas. Aguarde e tente novamente.", ApiErrorMapper.messageForCode("LOGIN_THROTTLED"))
    }

    @Test
    fun extractCode_reads_json_code() {
        assertEquals("LOGIN_THROTTLED", ApiErrorMapper.extractCode("""{"code":"LOGIN_THROTTLED","message":""}"""))
        assertNull(ApiErrorMapper.extractCode("{}"))
    }

    @Test
    fun uses_server_message_when_present() {
        assertEquals("Campo X", ApiErrorMapper.resolve("{\"code\":\"VALIDATION_ERROR\",\"message\":\"Campo X\"}"))
    }

    @Test
    fun falls_back_to_code_map_when_message_blank() {
        assertEquals(
            "Verifique os dados informados.",
            ApiErrorMapper.resolve("{\"code\":\"VALIDATION_ERROR\",\"message\":\"\"}"),
        )
    }

    @Test
    fun maps_lojista_invite_codes() {
        assertEquals(
            "Código do lojista ou ativação inválidos.",
            ApiErrorMapper.messageForCode("LOJISTA_INVITE_INVALID"),
        )
        assertEquals(
            "Este convite já foi utilizado.",
            ApiErrorMapper.messageForCode("LOJISTA_INVITE_CONSUMED"),
        )
    }

    @Test
    fun maps_lojista_grant_codes() {
        assertEquals(
            "Créditos insuficientes na sua carteira de convênio.",
            ApiErrorMapper.messageForCode("LOJISTA_CREDIT_INSUFFICIENT"),
        )
        assertEquals(
            "Esta placa está vinculada a outro convênio.",
            ApiErrorMapper.messageForCode("CLIENT_FOR_OTHER_LOJISTA"),
        )
        assertEquals(
            "É necessário ticket em aberto para esta placa, ou permita crédito antecipado na carteira.",
            ApiErrorMapper.messageForCode("GRANT_REQUIRES_ACTIVE_TICKET"),
        )
    }
}
