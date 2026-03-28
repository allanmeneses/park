package com.estacionamento.parking.offline

import com.squareup.moshi.Json

/** SPEC_FRONTEND §10 — item enfileirável (POST tickets / checkout). */
data class OfflineQueueItem(
    @Json(name = "id_local") val idLocal: String,
    val method: String = "POST",
    val path: String,
    @Json(name = "idempotency_key") val idempotencyKey: String,
    @Json(name = "body_json") val bodyJson: String?,
    @Json(name = "created_at_epoch") val createdAtEpoch: Long,
    val attempts: Int = 0,
)
