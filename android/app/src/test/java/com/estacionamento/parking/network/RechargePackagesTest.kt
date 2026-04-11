package com.estacionamento.parking.network

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class RechargePackagesTest {
    private val moshi: Moshi = Moshi.Builder()
        .add(KotlinJsonAdapterFactory())
        .build()

    private val adapter = moshi.adapter(RechargePackageDto::class.java)

    @Test
    fun parses_presentation_fields_from_api() {
        val json = """
            {
              "id":"pkg-1",
              "display_name":"Cliente 10h",
              "scope":"CLIENT",
              "hours":10,
              "price":"49.90",
              "is_promo":true,
              "sort_order":2,
              "active":false
            }
        """.trimIndent().replace("\n", "")

        val dto = adapter.fromJson(json)!!

        assertEquals("Cliente 10h", dto.displayName)
        assertTrue(dto.isPromo)
        assertEquals(2, dto.sortOrder)
        assertEquals(false, dto.active)
    }

    @Test
    fun sorts_like_web_contract() {
        val items = listOf(
            RechargePackageDto("3", "Sem promo", "CLIENT", 10, "19.90", isPromo = false, sortOrder = 1),
            RechargePackageDto("2", "Promo", "CLIENT", 10, "19.90", isPromo = true, sortOrder = 1),
            RechargePackageDto("1", "Primeiro", "CLIENT", 5, "29.90", isPromo = false, sortOrder = 0),
        )

        val ordered = items.sortedWith(RechargePackages::compare)

        assertEquals(listOf("1", "2", "3"), ordered.map { it.id })
        assertEquals("Promo", RechargePackages.title(ordered[1]))
    }
}
