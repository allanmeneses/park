package com.estacionamento.parking.network

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class SettingsAuditParsingTest {
    private val moshi: Moshi =
        Moshi.Builder()
            .add(KotlinJsonAdapterFactory())
            .build()

    @Test
    fun parses_settings_response_with_grant_validity_flag() {
        val adapter = moshi.adapter(SettingsResponse::class.java)
        val json =
            """
            {
              "price_per_hour":"5.00",
              "capacity":50,
              "lojista_grant_same_day_only":true
            }
            """.trimIndent().replace("\n", "")

        val dto = adapter.fromJson(json)!!

        assertEquals("5.00", dto.pricePerHour)
        assertEquals(50, dto.capacity)
        assertTrue(dto.lojistaGrantSameDayOnly)
    }

    @Test
    fun parses_settings_audit_response_items() {
        val adapter = moshi.adapter(SettingsAuditResponse::class.java)
        val json =
            """
            {
              "items":[
                {
                  "id":"evt-1",
                  "created_at":"2026-04-11T12:00:00Z",
                  "actor_email":"admin@test.com",
                  "actor_role":"ADMIN",
                  "changes":[
                    {
                      "field":"lojista_grant_same_day_only",
                      "label":"Validade da bonificação do lojista",
                      "from":"Prazo indeterminado",
                      "to":"Somente no dia da bonificação"
                    }
                  ]
                }
              ]
            }
            """.trimIndent().replace("\n", "")

        val dto = adapter.fromJson(json)!!

        assertEquals(1, dto.items.size)
        assertEquals("admin@test.com", dto.items.first().actorEmail)
        assertEquals("Validade da bonificação do lojista", dto.items.first().changes.first().label)
        assertEquals("Somente no dia da bonificação", dto.items.first().changes.first().to)
    }
}
