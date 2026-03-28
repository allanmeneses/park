package com.estacionamento.parking.auth

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

class AuthPrefs(context: Context) {
    private val masterKey = MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build()
    private val prefs = EncryptedSharedPreferences.create(
        context,
        "parking_auth_prefs",
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    var accessToken: String?
        get() = prefs.getString("access_token", null)
        set(v) {
            prefs.edit().putString("access_token", v).apply()
        }

    var refreshToken: String?
        get() = prefs.getString("refresh_token", null)
        set(v) {
            prefs.edit().putString("refresh_token", v).apply()
        }

    /** UUID do tenant (SUPER_ADMIN → header X-Parking-Id). SPEC §4.3 */
    var activeParkingId: String?
        get() = prefs.getString("active_parking_id", null)
        set(v) {
            if (v.isNullOrBlank()) {
                prefs.edit().remove("active_parking_id").apply()
            } else {
                prefs.edit().putString("active_parking_id", v.trim()).apply()
            }
        }

    /** Epoch UTC em segundos quando o access token expira (refresh proativo §3.3). */
    var accessTokenExpiresAtEpochSec: Long?
        get() {
            val v = prefs.getLong("access_exp_epoch_sec", 0L)
            return if (v > 0L) v else null
        }
        set(v) {
            if (v == null || v <= 0L) {
                prefs.edit().remove("access_exp_epoch_sec").apply()
            } else {
                prefs.edit().putLong("access_exp_epoch_sec", v).apply()
            }
        }

    fun clear() {
        prefs.edit().clear().apply()
    }
}
