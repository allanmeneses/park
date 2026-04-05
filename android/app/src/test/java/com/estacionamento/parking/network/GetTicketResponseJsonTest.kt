package com.estacionamento.parking.network

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class GetTicketResponseJsonTest {
    private val moshi: Moshi = Moshi.Builder()
        .add(KotlinJsonAdapterFactory())
        .build()

    private val adapter = moshi.adapter(GetTicketResponse::class.java)

    @Test
    fun parses_lojistaBenefits_array() {
        val json = """
            {
              "ticket":{
                "id":"550e8400-e29b-41d4-a716-446655440000",
                "plate":"AAA1111",
                "entryTime":"2026-04-03T12:00:00Z",
                "exitTime":null,
                "status":"OPEN",
                "createdAt":"2026-04-03T12:00:00Z"
              },
              "payment":null,
              "lojistaBenefits":[
                {"lojistaId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","lojistaName":"Loja X","hoursAvailable":2,"hoursGrantedTotal":3}
              ]
            }
        """.trimIndent().replace("\n", "")
        val r = adapter.fromJson(json)!!
        assertEquals(1, r.lojistaBenefits.size)
        val b = r.lojistaBenefits[0]
        assertEquals("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", b.lojistaId)
        assertEquals("Loja X", b.lojistaName)
        assertEquals(2, b.hoursAvailable)
        assertEquals(3, b.hoursGrantedTotal)
    }

    @Test
    fun parses_empty_lojistaBenefits() {
        val json = """
            {
              "ticket":{
                "id":"550e8400-e29b-41d4-a716-446655440000",
                "plate":"RCL9Z88",
                "entryTime":"2026-04-03T12:00:00Z",
                "exitTime":null,
                "status":"OPEN",
                "createdAt":"2026-04-03T12:00:00Z"
              },
              "payment":null,
              "lojistaBenefits":[]
            }
        """.trimIndent().replace("\n", "")
        val r = adapter.fromJson(json)!!
        assertTrue(r.lojistaBenefits.isEmpty())
    }
}
