package com.estacionamento.parking.offline

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

interface OfflineQueuePersistence {
    fun loadJson(): String
    fun saveJson(json: String)
}

private const val PREFS_NAME = "parking_offline_queue"
private const val KEY_QUEUE = "queue_json"

class EncryptedOfflineQueuePersistence(context: Context) : OfflineQueuePersistence {
    private val masterKey = MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build()
    private val prefs = EncryptedSharedPreferences.create(
        context,
        PREFS_NAME,
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    override fun loadJson(): String = prefs.getString(KEY_QUEUE, "[]") ?: "[]"

    override fun saveJson(json: String) {
        prefs.edit().putString(KEY_QUEUE, json).apply()
    }
}
