package com.estacionamento.parking.ui.mgr

import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.auth.JwtRoleParser
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.PspMercadoPagoPutBody
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

private fun webhookUrl(apiV1Base: String, parkingId: String?): String {
    val root = apiV1Base.trimEnd('/').removeSuffix("/api/v1").trimEnd('/')
    val pid = parkingId?.trim().orEmpty()
    if (pid.isBlank()) return ""
    return "$root/api/v1/payments/webhook/psp/mercadopago/$pid"
}

@Composable
fun MgrPspMercadoPagoScreen(
    api: ParkingApi,
    prefs: AuthPrefs,
    role: String,
    apiV1BaseUrl: String,
    onBack: () -> Unit,
) {
    val ctx = LocalContext.current
    val scope = rememberCoroutineScope()
    val canEdit = role == "ADMIN" || role == "SUPER_ADMIN"
    val scroll = rememberScrollState()

    var loading by remember { mutableStateOf(true) }
    var err by remember { mutableStateOf<String?>(null) }
    var useTenant by remember { mutableStateOf(false) }
    var environment by remember { mutableStateOf("PRODUCTION") }
    var accessToken by remember { mutableStateOf("") }
    var webhookSecret by remember { mutableStateOf("") }
    var publicKey by remember { mutableStateOf("") }
    var payerEmail by remember { mutableStateOf("") }
    var apiBaseUrl by remember { mutableStateOf("") }
    var checkoutBackSuccessUrl by remember { mutableStateOf("") }
    var checkoutBackFailureUrl by remember { mutableStateOf("") }
    var checkoutBackPendingUrl by remember { mutableStateOf("") }
    var acknowledged by remember { mutableStateOf(false) }
    var supportReason by remember { mutableStateOf("") }
    var hadAccessToken by remember { mutableStateOf(false) }
    var hadWebhookSecret by remember { mutableStateOf(false) }

    fun parkingIdForWebhook(): String? =
        prefs.activeParkingId?.takeIf { it.isNotBlank() }
            ?: prefs.accessToken?.let { JwtRoleParser.parkingIdFromAccessToken(it) }

    fun load() {
        scope.launch {
            err = null
            loading = true
            try {
                val d = api.pspMercadoPagoGet()
                useTenant = d.useTenantCredentials
                environment = d.environment.uppercase().let { if (it == "SANDBOX") "SANDBOX" else "PRODUCTION" }
                publicKey = d.publicKey
                payerEmail = d.payerEmail
                apiBaseUrl = d.apiBaseUrl.orEmpty()
                checkoutBackSuccessUrl = d.checkoutBackSuccessUrl.orEmpty()
                checkoutBackFailureUrl = d.checkoutBackFailureUrl.orEmpty()
                checkoutBackPendingUrl = d.checkoutBackPendingUrl.orEmpty()
                hadAccessToken = d.hasAccessToken
                hadWebhookSecret = d.hasWebhookSecret
                accessToken = ""
                webhookSecret = ""
                acknowledged = false
                supportReason = ""
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            } finally {
                loading = false
            }
        }
    }

    LaunchedEffect(Unit) {
        load()
    }

    Column(Modifier.padding(16.dp).verticalScroll(scroll)) {
        Text(UiStrings.B37, style = MaterialTheme.typography.titleLarge)
        Text(
            "Sem credenciais do tenant, aplicam-se as variáveis globais MERCADOPAGO_* do servidor.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        if (!canEdit) {
            Text(
                "Apenas ADMIN ou SUPER_ADMIN pode gravar.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(bottom = 8.dp),
            )
        }
        if (loading) {
            Text(UiStrings.S32)
            Button(onClick = onBack, modifier = Modifier.padding(top = 12.dp)) { Text(UiStrings.Voltar) }
        } else {
        RowCheck(
            checked = useTenant,
            onCheckedChange = { if (canEdit) useTenant = it },
            enabled = canEdit,
            label = "Usar credenciais Mercado Pago deste estacionamento",
        )
        if (useTenant) {
            OutlinedTextField(
                value = environment,
                onValueChange = { v ->
                    val u = v.uppercase()
                    if (u == "SANDBOX" || u == "PRODUCTION") environment = u
                },
                label = { Text("Ambiente (SANDBOX ou PRODUCTION)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            OutlinedTextField(
                value = accessToken,
                onValueChange = { accessToken = it },
                label = { Text("Access token") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = false,
                enabled = canEdit,
            )
            if (hadAccessToken) {
                Text(
                    "Já existe token guardado; preencha de novo para substituir.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            OutlinedTextField(
                value = webhookSecret,
                onValueChange = { webhookSecret = it },
                label = { Text("Segredo do webhook") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = false,
                enabled = canEdit,
            )
            if (hadWebhookSecret) {
                Text(
                    "Já existe segredo guardado; preencha de novo para substituir.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            OutlinedTextField(
                value = publicKey,
                onValueChange = { publicKey = it },
                label = { Text("Chave pública") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            OutlinedTextField(
                value = payerEmail,
                onValueChange = { payerEmail = it },
                label = { Text("E-mail do pagador") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            OutlinedTextField(
                value = apiBaseUrl,
                onValueChange = { apiBaseUrl = it },
                label = { Text("URL base API MP (opcional)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            OutlinedTextField(
                value = checkoutBackSuccessUrl,
                onValueChange = { checkoutBackSuccessUrl = it },
                label = { Text("URL volta sucesso (opcional)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            OutlinedTextField(
                value = checkoutBackFailureUrl,
                onValueChange = { checkoutBackFailureUrl = it },
                label = { Text("URL volta falha (opcional)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            OutlinedTextField(
                value = checkoutBackPendingUrl,
                onValueChange = { checkoutBackPendingUrl = it },
                label = { Text("URL volta pendente (opcional)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
                enabled = canEdit,
            )
            RowCheck(
                checked = acknowledged,
                onCheckedChange = { if (canEdit) acknowledged = it },
                enabled = canEdit,
                label = "Confirmo responsabilidade pelas credenciais desta conta Mercado Pago.",
            )
        }
        if (role == "SUPER_ADMIN" && canEdit) {
            OutlinedTextField(
                value = supportReason,
                onValueChange = { supportReason = it },
                label = { Text("Motivo (obrigatório SUPER_ADMIN)") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
            )
        }
        Text(
            "Webhook POST no Mercado Pago:",
            style = MaterialTheme.typography.titleSmall,
            modifier = Modifier.padding(top = 12.dp),
        )
        val wh = webhookUrl(apiV1BaseUrl, parkingIdForWebhook())
        Text(
            if (wh.isBlank()) {
                "Defina o estacionamento ativo (super admin) ou inicie sessão no tenant para ver o URL."
            } else {
                wh
            },
            style = MaterialTheme.typography.bodySmall,
            modifier = Modifier.padding(top = 4.dp),
        )
        Button(onClick = onBack, modifier = Modifier.padding(top = 12.dp)) { Text(UiStrings.Voltar) }
        if (canEdit) {
            Button(
                onClick = {
                    err = null
                    if (role == "SUPER_ADMIN" && supportReason.trim().isEmpty()) {
                        err = "SUPER_ADMIN deve indicar o motivo."
                        return@Button
                    }
                    if (useTenant) {
                        if (!acknowledged) {
                            err = "Confirme a responsabilidade (checkbox)."
                            return@Button
                        }
                        if (accessToken.trim().isEmpty() || webhookSecret.trim().isEmpty() ||
                            publicKey.trim().isEmpty() || payerEmail.trim().isEmpty()
                        ) {
                            err = "Access token, segredo webhook, chave pública e e-mail são obrigatórios."
                            return@Button
                        }
                    }
                    scope.launch {
                        try {
                            api.pspMercadoPagoPut(
                                PspMercadoPagoPutBody(
                                    useTenantCredentials = useTenant,
                                    acknowledged = if (useTenant) acknowledged else false,
                                    environment = if (useTenant) environment else null,
                                    accessToken = if (useTenant) accessToken.trim() else null,
                                    webhookSecret = if (useTenant) webhookSecret.trim() else null,
                                    publicKey = if (useTenant) publicKey.trim() else null,
                                    payerEmail = if (useTenant) payerEmail.trim() else null,
                                    apiBaseUrl = if (useTenant && apiBaseUrl.isNotBlank()) apiBaseUrl.trim() else null,
                                    checkoutBackSuccessUrl =
                                        if (useTenant && checkoutBackSuccessUrl.isNotBlank()) {
                                            checkoutBackSuccessUrl.trim()
                                        } else {
                                            null
                                        },
                                    checkoutBackFailureUrl =
                                        if (useTenant && checkoutBackFailureUrl.isNotBlank()) {
                                            checkoutBackFailureUrl.trim()
                                        } else {
                                            null
                                        },
                                    checkoutBackPendingUrl =
                                        if (useTenant && checkoutBackPendingUrl.isNotBlank()) {
                                            checkoutBackPendingUrl.trim()
                                        } else {
                                            null
                                        },
                                    supportReason = if (role == "SUPER_ADMIN") supportReason.trim() else null,
                                ),
                            )
                            Toast.makeText(ctx, UiStrings.T11, Toast.LENGTH_SHORT).show()
                            load()
                        } catch (e: HttpException) {
                            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                        } catch (e: Exception) {
                            err = e.message
                        }
                    }
                },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            ) {
                Text(UiStrings.Salvar)
            }
        }
        }
    }
}

@Composable
private fun RowCheck(
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    enabled: Boolean,
    label: String,
) {
    Row(Modifier.padding(top = 8.dp)) {
        Checkbox(checked = checked, onCheckedChange = onCheckedChange, enabled = enabled)
        Text(label, modifier = Modifier.padding(top = 12.dp, start = 4.dp))
    }
}
