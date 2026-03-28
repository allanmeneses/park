package com.estacionamento.parking.offline

import com.squareup.moshi.Moshi
import com.squareup.moshi.Types
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory

object OfflineQueueJson {
    private val moshi = Moshi.Builder().add(KotlinJsonAdapterFactory()).build()
    private val listType = Types.newParameterizedType(List::class.java, OfflineQueueItem::class.java)
    private val adapter = moshi.adapter<List<OfflineQueueItem>>(listType)

    fun toJson(items: List<OfflineQueueItem>): String = adapter.toJson(items)

    fun fromJson(json: String): List<OfflineQueueItem> {
        if (json.isBlank()) return emptyList()
        return try {
            adapter.fromJson(json).orEmpty()
        } catch (_: Exception) {
            emptyList()
        }
    }
}
