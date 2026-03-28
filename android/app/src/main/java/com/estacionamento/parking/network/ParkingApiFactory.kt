package com.estacionamento.parking.network

import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.auth.JwtRoleParser
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory
import java.util.concurrent.TimeUnit

data class ParkingHttpStack(
    val api: ParkingApi,
    val okHttpClient: OkHttpClient,
    val rootBaseUrl: String,
    val authRefresh: ParkingAuthRefresh,
)

object ParkingApiFactory {
    private val jsonMedia = "application/json; charset=utf-8".toMediaType()

    val emptyJsonBody: RequestBody = "{}".toByteArray(Charsets.UTF_8).toRequestBody(jsonMedia)

    private fun moshi(): Moshi = Moshi.Builder().add(KotlinJsonAdapterFactory()).build()

    fun createStack(baseUrl: String, prefs: AuthPrefs): ParkingHttpStack {
        val m = moshi()
        val root = baseUrl.trimEnd('/')
        val retrofitBase = "$root/"

        val plainClient = OkHttpClient.Builder()
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .build()

        val refreshRetrofit = Retrofit.Builder()
            .baseUrl(retrofitBase)
            .client(plainClient)
            .addConverterFactory(MoshiConverterFactory.create(m))
            .build()
        val authRefresh = refreshRetrofit.create(ParkingAuthRefresh::class.java)

        val authClient = OkHttpClient.Builder()
            .authenticator(ParkingTokenAuthenticator(prefs, authRefresh))
            .addInterceptor { chain ->
                val b = chain.request().newBuilder()
                prefs.accessToken?.let { b.header("Authorization", "Bearer $it") }
                val role = prefs.accessToken?.let { JwtRoleParser.roleFromAccessToken(it) }
                if (role == "SUPER_ADMIN" && !prefs.activeParkingId.isNullOrBlank()) {
                    b.header("X-Parking-Id", prefs.activeParkingId!!)
                }
                chain.proceed(b.build())
            }
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .build()

        val retrofit = Retrofit.Builder()
            .baseUrl(retrofitBase)
            .client(authClient)
            .addConverterFactory(MoshiConverterFactory.create(m))
            .build()

        return ParkingHttpStack(
            api = retrofit.create(ParkingApi::class.java),
            okHttpClient = authClient,
            rootBaseUrl = root,
            authRefresh = authRefresh,
        )
    }

    fun create(baseUrl: String, prefs: AuthPrefs): ParkingApi = createStack(baseUrl, prefs).api
}
