package com.estacionamento.parking.network

import retrofit2.http.Body
import retrofit2.http.POST

/** Cliente sem Bearer — SPEC §3.3 refresh (evita loop 401 no OkHttp principal). */
interface ParkingAuthRefresh {
    @POST("auth/refresh")
    suspend fun refresh(@Body body: RefreshBody): LoginResponse
}
