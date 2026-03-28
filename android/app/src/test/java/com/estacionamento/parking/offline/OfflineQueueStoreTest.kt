package com.estacionamento.parking.offline

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

class OfflineQueueStoreTest {

    private lateinit var persistence: FakeOfflineQueuePersistence
    private lateinit var store: OfflineQueueStore

    @Before
    fun setup() {
        persistence = FakeOfflineQueuePersistence()
        store = OfflineQueueStore(persistence)
    }

    @Test
    fun enqueue_then_peek_fifo_order() {
        val a = sampleItem("a", "tickets")
        val b = sampleItem("b", "tickets/uuid/checkout")
        store.enqueue(a)
        store.enqueue(b)
        assertEquals(2, store.size())
        assertEquals("a", store.peekOrNull()?.idLocal)
    }

    @Test
    fun removeHead_pops_fifo() {
        store.enqueue(sampleItem("1", "tickets"))
        store.enqueue(sampleItem("2", "tickets"))
        store.removeHead()
        assertEquals("2", store.peekOrNull()?.idLocal)
        assertEquals(1, store.size())
    }

    @Test
    fun updateHeadAttempts_persists() {
        store.enqueue(sampleItem("x", "tickets", attempts = 0))
        store.updateHeadAttempts(3)
        val reloaded = OfflineQueueStore(persistence)
        assertEquals(3, reloaded.peekOrNull()?.attempts)
    }

    @Test
    fun json_roundtrip_preserves_fields() {
        val item = OfflineQueueItem(
            idLocal = "loc1",
            path = "tickets",
            idempotencyKey = "idem-1",
            bodyJson = """{"plate":"ABC1D23"}""",
            createdAtEpoch = 1700000000L,
            attempts = 2,
        )
        val json = OfflineQueueJson.toJson(listOf(item))
        val back = OfflineQueueJson.fromJson(json)
        assertEquals(1, back.size)
        assertEquals(item.idLocal, back[0].idLocal)
        assertEquals(item.path, back[0].path)
        assertEquals(item.idempotencyKey, back[0].idempotencyKey)
        assertEquals(item.bodyJson, back[0].bodyJson)
        assertEquals(item.createdAtEpoch, back[0].createdAtEpoch)
        assertEquals(item.attempts, back[0].attempts)
        assertEquals("POST", back[0].method)
    }

    private fun sampleItem(id: String, path: String, attempts: Int = 0) = OfflineQueueItem(
        idLocal = id,
        path = path,
        idempotencyKey = "k-$id",
        bodyJson = "{}",
        createdAtEpoch = 1L,
        attempts = attempts,
    )
}

class FakeOfflineQueuePersistence : OfflineQueuePersistence {
    var raw: String = "[]"
    override fun loadJson(): String = raw
    override fun saveJson(json: String) {
        raw = json
    }
}
