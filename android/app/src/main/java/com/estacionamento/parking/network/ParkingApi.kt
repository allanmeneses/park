package com.estacionamento.parking.network

import com.squareup.moshi.Json
import okhttp3.RequestBody
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

data class LoginBody(val email: String, val password: String)

data class RegisterLojistaBody(
    val merchantCode: String,
    val activationCode: String,
    val email: String,
    val password: String,
    val name: String,
)

data class LojistaInviteCreateBody(val displayName: String? = null)

data class LojistaInviteCreateResponse(
    val merchantCode: String,
    val activationCode: String,
    val lojistaId: String,
)

data class LojistaInviteListItemDto(
    val merchantCode: String? = null,
    val lojistaId: String? = null,
    val shopName: String? = null,
    val createdAt: String? = null,
    val activated: Boolean = false,
    val email: String? = null,
    val totalPurchasedHours: Int? = null,
    val balanceHours: Int? = null,
)

data class LojistaInvitesListResponse(val items: List<LojistaInviteListItemDto> = emptyList())

data class GrantClientBody(
    val plate: String? = null,
    @Json(name = "ticketId") val ticketId: String? = null,
    val hours: Int? = null,
)

data class LojistaGrantClientResponse(
    @Json(name = "grant_id") val grantId: String,
    val plate: String,
    val hours: Int,
    @Json(name = "grant_mode") val grantMode: String = "ADVANCE",
    @Json(name = "client_balance_hours") val clientBalanceHours: Int,
    @Json(name = "lojista_balance_hours") val lojistaBalanceHours: Int,
)

data class LojistaGrantHistoryItemDto(
    val id: String,
    @Json(name = "created_at") val createdAt: String,
    val plate: String,
    val hours: Int,
    @Json(name = "grant_mode") val grantMode: String = "ADVANCE",
    @Json(name = "client_id") val clientId: String? = null,
)

data class LojistaGrantHistoryResponse(
    val items: List<LojistaGrantHistoryItemDto> = emptyList(),
)

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

data class LojistaBenefitDto(
    @Json(name = "lojistaId") val lojistaId: String,
    @Json(name = "lojistaName") val lojistaName: String,
    @Json(name = "hoursAvailable") val hoursAvailable: Int,
    @Json(name = "hoursGrantedTotal") val hoursGrantedTotal: Int,
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
    @Json(name = "lojistaBenefits") val lojistaBenefits: List<LojistaBenefitDto> = emptyList(),
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

/** Resposta de POST /payments/card — stub (PAID) ou checkout hospedado (Mercado Pago). */
data class CardPayResponse(
    @Json(name = "payment_id") val paymentId: String? = null,
    val mode: String? = null,
    val status: String? = null,
    val provider: String? = null,
    @Json(name = "preference_id") val preferenceId: String? = null,
    @Json(name = "init_point") val initPoint: String? = null,
    @Json(name = "sandbox_init_point") val sandboxInitPoint: String? = null,
    @Json(name = "public_key") val publicKey: String? = null,
)

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

data class MovementInsightsDto(
    @Json(name = "total_ticket") val totalTicket: String,
    @Json(name = "total_package") val totalPackage: String,
    @Json(name = "usages_lojista") val usagesLojista: Int,
    @Json(name = "usages_client") val usagesClient: Int,
)

data class MovementItemDto(
    @Json(name = "at") val at: String,
    @Json(name = "kind") val kind: String,
    @Json(name = "amount") val amount: String,
    @Json(name = "ref") val ref: String,
    @Json(name = "method") val method: String?,
    @Json(name = "lojista_id") val lojistaId: String? = null,
    @Json(name = "ticket_split_type") val ticketSplitType: String? = null,
    @Json(name = "hours_lojista") val hoursLojista: Int = 0,
    @Json(name = "hours_cliente") val hoursCliente: Int = 0,
    @Json(name = "hours_direct") val hoursDirect: Int = 0,
)

data class ManagerMovementsResponse(
    @Json(name = "from") val from: String,
    @Json(name = "to") val to: String,
    @Json(name = "count") val count: Int,
    @Json(name = "insights") val insights: MovementInsightsDto,
    @Json(name = "items") val items: List<MovementItemDto> = emptyList(),
)

data class AnalyticsTotalsDto(
    @Json(name = "revenue") val revenue: String,
    @Json(name = "payments") val payments: Int,
    @Json(name = "checkouts") val checkouts: Int,
)

data class AnalyticsTrendRowDto(
    @Json(name = "day") val day: String,
    @Json(name = "amount") val amount: String,
    @Json(name = "payments") val payments: Int,
)

data class AnalyticsHourlyRowDto(
    @Json(name = "hour") val hour: Int,
    @Json(name = "amount") val amount: String,
    @Json(name = "payments") val payments: Int,
)

data class AnalyticsPeakRowDto(
    @Json(name = "hour") val hour: Int,
    @Json(name = "checkouts") val checkouts: Int,
)

data class ManagerAnalyticsResponse(
    @Json(name = "days") val days: Int,
    @Json(name = "totals") val totals: AnalyticsTotalsDto,
    @Json(name = "trend_by_day") val trendByDay: List<AnalyticsTrendRowDto> = emptyList(),
    @Json(name = "gains_by_hour") val gainsByHour: List<AnalyticsHourlyRowDto> = emptyList(),
    @Json(name = "peak_hours") val peakHours: List<AnalyticsPeakRowDto> = emptyList(),
)

data class BalancesReportLojistaRowDto(
    @Json(name = "lojistaId") val lojistaId: String,
    @Json(name = "lojistaName") val lojistaName: String?,
    @Json(name = "balanceHours") val balanceHours: Int,
)

data class BalancesReportClientPlateRowDto(
    val plate: String,
    @Json(name = "balanceHours") val balanceHours: Int,
    @Json(name = "expirationDate") val expirationDate: String?,
)

data class BalancesReportBonificadoPlateRowDto(
    val plate: String,
    @Json(name = "balanceHours") val balanceHours: Int,
)

data class ManagerBalancesReportResponse(
    val lojistas: List<BalancesReportLojistaRowDto> = emptyList(),
    @Json(name = "lojistaBonificadoPlates") val lojistaBonificadoPlates: List<BalancesReportBonificadoPlateRowDto> = emptyList(),
    @Json(name = "clientPlates") val clientPlates: List<BalancesReportClientPlateRowDto> = emptyList(),
)

data class ClientWalletResponse(
    @Json(name = "balance_hours") val balanceHours: Int,
    @Json(name = "expiration_date") val expirationDate: String?,
)

data class LojWalletResponse(
    @Json(name = "balance_hours") val balanceHours: Int,
)

data class LojistaGrantSettingsResponse(
    @Json(name = "allow_grant_before_entry") val allowGrantBeforeEntry: Boolean,
)

data class LojistaGrantSettingsBody(
    @Json(name = "allow_grant_before_entry") val allowGrantBeforeEntry: Boolean,
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

data class AdminTenantListItem(
    val parkingId: String,
    val label: String,
)

data class AdminTenantsResponse(val items: List<AdminTenantListItem> = emptyList())

data class AdminCreateTenantBody(
    val parkingId: String? = null,
    val adminEmail: String,
    val adminPassword: String,
    val operatorEmail: String,
    val operatorPassword: String,
)

data class AdminCreateTenantResponse(
    val parkingId: String,
    val databaseName: String,
    val adminUserId: String,
    val operatorUserId: String,
)

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

    @POST("auth/register-lojista")
    suspend fun registerLojista(@Body body: RegisterLojistaBody): LoginResponse

    @GET("admin/lojista-invites")
    suspend fun lojistaInvites(): LojistaInvitesListResponse

    @POST("admin/lojista-invites")
    suspend fun lojistaInvitesCreate(@Body body: LojistaInviteCreateBody): LojistaInviteCreateResponse

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
    suspend fun payCard(@Body body: CardPayBody): CardPayResponse

    @POST("payments/cash")
    suspend fun payCash(@Body body: CashPayBody): PaymentStatusResponse

    @POST("operator/problem")
    suspend fun operatorProblem(@Body body: RequestBody)

    @GET("dashboard")
    suspend fun dashboard(): DashboardResponse

    @GET("manager/movements")
    suspend fun managerMovements(
        @Query("from") from: String? = null,
        @Query("to") to: String? = null,
        @Query("kind") kind: String? = null,
        @Query("lojista_id") lojistaId: String? = null,
        @Query("limit") limit: Int = 200,
    ): ManagerMovementsResponse

    @GET("manager/analytics")
    suspend fun managerAnalytics(@Query("days") days: Int = 14): ManagerAnalyticsResponse

    @GET("manager/balances-report")
    suspend fun managerBalancesReport(@Query("plate") plate: String? = null): ManagerBalancesReportResponse

    @GET("client/wallet")
    suspend fun clientWallet(): ClientWalletResponse

    @GET("client/history")
    suspend fun clientHistory(
        @Query("limit") limit: Int = 50,
        @Query("cursor") cursor: String? = null,
    ): HistoryResponse

    @GET("lojista/wallet")
    suspend fun lojistaWallet(): LojWalletResponse

    @GET("lojista/grant-settings")
    suspend fun lojistaGrantSettings(): LojistaGrantSettingsResponse

    @PUT("lojista/grant-settings")
    suspend fun lojistaPutGrantSettings(@Body body: LojistaGrantSettingsBody): LojistaGrantSettingsResponse

    @GET("lojista/history")
    suspend fun lojistaHistory(
        @Query("limit") limit: Int = 50,
        @Query("cursor") cursor: String? = null,
    ): HistoryResponse

    @POST("lojista/grant-client")
    suspend fun lojistaGrantClient(
        @Header("Idempotency-Key") idem: String,
        @Body body: GrantClientBody,
    ): LojistaGrantClientResponse

    @GET("lojista/grant-client/history")
    suspend fun lojistaGrantHistory(
        @Query("from") from: String? = null,
        @Query("to") to: String? = null,
        @Query("plate") plate: String? = null,
        @Query("limit") limit: Int? = null,
    ): LojistaGrantHistoryResponse

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

    @GET("admin/tenants")
    suspend fun adminTenants(): AdminTenantsResponse

    @POST("admin/tenants")
    suspend fun adminCreateTenant(@Body body: AdminCreateTenantBody): AdminCreateTenantResponse
}
