package com.estacionamento.parking.network

import com.squareup.moshi.Json
import okhttp3.RequestBody
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

data class LoginBody(val email: String, val password: String)

data class LoginResponse(
    @Json(name = "access_token") val accessToken: String,
    @Json(name = "refresh_token") val refreshToken: String,
    @Json(name = "expires_in") val expiresIn: Int,
)

data class RefreshBody(@Json(name = "refresh_token") val refreshToken: String)

data class OpenTicketsResponse(val items: List<TicketOpenItem> = emptyList())

data class TicketOpenItem(
    val id: String,
    val plate: String,
    @Json(name = "entryTime") val entryTime: String,
    val status: String,
)

data class CreateTicketBody(val plate: String)

data class CreateTicketResponse(
    val id: String,
    val plate: String,
    val status: String,
    @Json(name = "entry_time") val entryTime: String? = null,
)

data class TicketDetailDto(
    val id: String,
    val plate: String,
    @Json(name = "entryTime") val entryTime: String,
    @Json(name = "exitTime") val exitTime: String?,
    val status: String,
    @Json(name = "createdAt") val createdAt: String,
)

data class PaymentPixDto(
    @Json(name = "expires_at") val expiresAt: String,
    val active: Boolean,
)

data class PaymentDetailDto(
    val id: String,
    val status: String,
    val method: String?,
    val amount: String,
    @Json(name = "ticket_id") val ticketId: String?,
    @Json(name = "package_order_id") val packageOrderId: String?,
    @Json(name = "paid_at") val paidAt: String?,
    @Json(name = "created_at") val createdAt: String,
    @Json(name = "failed_reason") val failedReason: String?,
    val pix: PaymentPixDto?,
)

data class GetTicketResponse(
    val ticket: TicketDetailDto,
    val payment: PaymentDetailDto?,
)

data class CheckoutResponse(
    @Json(name = "ticket_id") val ticketId: String,
    @Json(name = "hours_total") val hoursTotal: Int,
    @Json(name = "hours_lojista") val hoursLojista: Int,
    @Json(name = "hours_cliente") val hoursCliente: Int,
    @Json(name = "hours_paid") val hoursPaid: Int,
    val amount: String,
    @Json(name = "payment_id") val paymentId: String,
)

data class PixChargeResponse(
    @Json(name = "payment_id") val paymentId: String,
    @Json(name = "qr_code") val qrCode: String,
    @Json(name = "expires_at") val expiresAt: String,
)

data class PixPayBody(@Json(name = "payment_id") val paymentId: String)

data class CardPayBody(val paymentId: String, val amount: Double)

data class CashPayBody(val paymentId: String)

data class PaymentStatusResponse(
    @Json(name = "payment_id") val paymentId: String,
    val status: String,
)

data class DashboardResponse(
    val faturamento: Double,
    val ocupacao: Double,
    @Json(name = "tickets_dia") val ticketsDia: Int,
    @Json(name = "uso_convenio") val usoConvenio: Double?,
)

data class ClientWalletResponse(
    @Json(name = "balance_hours") val balanceHours: Int,
    @Json(name = "expiration_date") val expirationDate: String?,
)

data class LojWalletResponse(
    @Json(name = "balance_hours") val balanceHours: Int,
)

data class HistoryRefDto(val type: String, val id: String)

data class HistoryItemDto(
    val id: String,
    val kind: String,
    @Json(name = "delta_hours") val deltaHours: Int,
    val amount: String,
    @Json(name = "created_at") val createdAt: String,
    @Json(name = "ref") val ref: HistoryRefDto?,
)

data class HistoryResponse(
    val items: List<HistoryItemDto> = emptyList(),
    @Json(name = "next_cursor") val nextCursor: String? = null,
)

data class SettingsResponse(
    @Json(name = "price_per_hour") val pricePerHour: String,
    val capacity: Int,
)

data class SettingsPostBody(val pricePerHour: Double, val capacity: Int)

data class SettingsOkResponse(val ok: Boolean)

data class RechargePackageDto(
    val id: String,
    val scope: String,
    val hours: Int,
    val price: String,
)

data class RechargePackagesResponse(val items: List<RechargePackageDto> = emptyList())

data class ClientBuyBody(val packageId: String, val settlement: String)

data class PackageBuyResponse(
    @Json(name = "order_id") val orderId: String,
    val status: String,
    @Json(name = "balance_hours") val balanceHours: Int? = null,
    @Json(name = "payment_id") val paymentId: String? = null,
)

data class CashOpenResponse(
    @Json(name = "session_id") val sessionId: String,
    @Json(name = "opened_at") val openedAt: String,
)

data class CashOpenInfoDto(
    @Json(name = "session_id") val sessionId: String,
    @Json(name = "opened_at") val openedAt: String,
    @Json(name = "expected_amount") val expectedAmount: String,
)

data class CashLastClosedDto(
    @Json(name = "session_id") val sessionId: String,
    @Json(name = "expected_amount") val expectedAmount: String,
    @Json(name = "actual_amount") val actualAmount: String?,
)

data class CashGetResponse(
    val open: CashOpenInfoDto?,
    @Json(name = "last_closed") val lastClosed: CashLastClosedDto?,
)

data class CashCloseBody(
    val sessionId: String,
    val actualAmount: Double,
)

data class CashCloseResponse(
    @Json(name = "session_id") val sessionId: String,
    @Json(name = "expected_amount") val expectedAmount: String,
    @Json(name = "actual_amount") val actualAmount: String,
    val divergence: Double,
    val alert: Boolean,
)

interface ParkingApi {
    @POST("auth/login")
    suspend fun login(@Body body: LoginBody): LoginResponse

    @GET("tickets/open")
    suspend fun openTickets(): OpenTicketsResponse

    @GET("tickets/{id}")
    suspend fun getTicket(@Path("id") id: String): GetTicketResponse

    @POST("tickets")
    suspend fun createTicket(
        @Header("Idempotency-Key") idem: String,
        @Body body: CreateTicketBody,
    ): CreateTicketResponse

    @POST("tickets/{id}/checkout")
    suspend fun checkout(
        @Path("id") ticketId: String,
        @Header("Idempotency-Key") idem: String,
        @Body body: RequestBody,
    ): CheckoutResponse

    @GET("payments/{id}")
    suspend fun getPayment(@Path("id") id: String): PaymentDetailDto

    @POST("payments/pix")
    suspend fun payPix(@Body body: PixPayBody): PixChargeResponse

    @POST("payments/card")
    suspend fun payCard(@Body body: CardPayBody): PaymentStatusResponse

    @POST("payments/cash")
    suspend fun payCash(@Body body: CashPayBody): PaymentStatusResponse

    @POST("operator/problem")
    suspend fun operatorProblem(@Body body: RequestBody)

    @GET("dashboard")
    suspend fun dashboard(): DashboardResponse

    @GET("client/wallet")
    suspend fun clientWallet(): ClientWalletResponse

    @GET("client/history")
    suspend fun clientHistory(
        @Query("limit") limit: Int = 50,
        @Query("cursor") cursor: String? = null,
    ): HistoryResponse

    @GET("lojista/wallet")
    suspend fun lojistaWallet(): LojWalletResponse

    @GET("lojista/history")
    suspend fun lojistaHistory(
        @Query("limit") limit: Int = 50,
        @Query("cursor") cursor: String? = null,
    ): HistoryResponse

    @POST("client/buy")
    suspend fun clientBuy(
        @Header("Idempotency-Key") idem: String,
        @Body body: ClientBuyBody,
    ): PackageBuyResponse

    @POST("lojista/buy")
    suspend fun lojistaBuy(
        @Header("Idempotency-Key") idem: String,
        @Body body: ClientBuyBody,
    ): PackageBuyResponse

    @GET("cash")
    suspend fun cashStatus(): CashGetResponse

    @POST("cash/open")
    suspend fun cashOpen(): CashOpenResponse

    @POST("cash/close")
    suspend fun cashClose(@Body body: CashCloseBody): CashCloseResponse

    @GET("settings")
    suspend fun settings(): SettingsResponse

    @POST("settings")
    suspend fun settingsPost(@Body body: SettingsPostBody): SettingsOkResponse

    @GET("recharge-packages")
    suspend fun rechargePackages(@Query("scope") scope: String): RechargePackagesResponse
}
