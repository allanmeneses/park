package com.estacionamento.parking.ui.op

import android.content.Intent
import android.net.Uri
import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.CardPayBody
import com.estacionamento.parking.network.CardPayOutcome
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.toOutcome
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun OpPayCardScreen(
    api: ParkingApi,
    paymentId: String,
    /** Em debug, preferir `sandbox_init_point` do Mercado Pago quando existir. */
    preferSandboxCheckoutUrl: Boolean,
    onSuccess: () -> Unit,
    onAmountMismatch: () -> Unit,
    onBack: () -> Unit,
) {
    val ctx = LocalContext.current
    var amount by remember { mutableStateOf<String?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var hostedUrl by remember { mutableStateOf<String?>(null) }
    var pollGen by remember { mutableIntStateOf(0) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(paymentId) {
        try {
            amount = api.getPayment(paymentId).amount
        } catch (e: HttpException) {
            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
        } catch (e: Exception) {
            err = e.message
        }
    }

    LaunchedEffect(pollGen) {
        if (pollGen == 0) return@LaunchedEffect
        val deadline = System.currentTimeMillis() + 900_000L
        while (isActive && System.currentTimeMillis() < deadline) {
            delay(2_000)
            try {
                when (api.getPayment(paymentId).status) {
                    "PAID" -> {
                        Toast.makeText(ctx, UiStrings.T4, Toast.LENGTH_SHORT).show()
                        onSuccess()
                        return@LaunchedEffect
                    }
                    "FAILED" -> {
                        err = UiStrings.E7
                        return@LaunchedEffect
                    }
                    "EXPIRED" -> {
                        err = UiStrings.S28
                        return@LaunchedEffect
                    }
                }
            } catch (_: Exception) { }
        }
        err = UiStrings.S28
    }

    fun openCheckoutUrl() {
        val u = hostedUrl ?: return
        ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(u)))
    }

    Column(Modifier.padding(16.dp)) {
        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {
            Text(UiStrings.Voltar)
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        amount?.let { Text("Valor: R\$ $it") }
        hostedUrl?.let {
            Text(UiStrings.S27, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(vertical = 8.dp))
            Button(
                onClick = { openCheckoutUrl() },
                modifier = Modifier
                    .fillMaxWidth()
                    .semantics { contentDescription = UiStrings.B33 },
            ) {
                Text(UiStrings.B33)
            }
        }
        Button(
            onClick = {
                val a = amount?.toDoubleOrNull() ?: return@Button
                scope.launch {
                    try {
                        val r = api.payCard(CardPayBody(paymentId, a))
                        when (val outcome = r.toOutcome(preferSandboxCheckoutUrl)) {
                            null -> err = "Resposta do servidor não reconhecida."
                            is CardPayOutcome.SyncPaid -> onSuccess()
                            is CardPayOutcome.HostedCheckout -> {
                                hostedUrl = outcome.openUrl
                                ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(outcome.openUrl)))
                                pollGen++
                            }
                        }
                    } catch (e: HttpException) {
                        val body = e.response()?.errorBody()?.string().orEmpty()
                        if (body.contains("AMOUNT_MISMATCH")) onAmountMismatch()
                        else err = ApiErrorMapper.resolve(body)
                    } catch (e: Exception) {
                        err = e.message
                    }
                }
            },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 16.dp)
                .semantics { contentDescription = UiStrings.Confirmar },
            enabled = amount != null && hostedUrl == null,
        ) {
            Text(UiStrings.Confirmar)
        }
    }
}
