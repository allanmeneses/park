package com.estacionamento.parking.ui.op

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.CashGetResponse
import com.estacionamento.parking.network.CashPayBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.ParkingApiFactory
import com.estacionamento.parking.network.PaymentDetailDto
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.UUID

@Composable
fun OpPayMethodScreen(
    api: ParkingApi,
    paymentId: String,
    onPix: () -> Unit,
    onCard: () -> Unit,
    onCashSuccess: () -> Unit,
    /** Checkout com valor zero (convênio/carteira) — mesmo fluxo que sair sem PIX/cartão/dinheiro. */
    onNothingToPay: () -> Unit,
    onBack: () -> Unit,
) {
    var cash by remember { mutableStateOf<CashGetResponse?>(null) }
    var payment by remember { mutableStateOf<PaymentDetailDto?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var showCashConfirm by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(paymentId) {
        err = null
        cash = try {
            api.cashStatus()
        } catch (e: HttpException) {
            if (e.code() == 403) CashGetResponse(open = null, lastClosed = null) else {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                return@LaunchedEffect
            }
        } catch (e: Exception) {
            err = e.message
            return@LaunchedEffect
        }

        var p: PaymentDetailDto
        try {
            p = api.getPayment(paymentId)
        } catch (e: HttpException) {
            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            return@LaunchedEffect
        } catch (e: Exception) {
            err = e.message
            return@LaunchedEffect
        }

        fun paymentSettled(d: PaymentDetailDto): Boolean =
            d.status.equals("PAID", ignoreCase = true) ||
                (d.status.equals("PENDING", ignoreCase = true) &&
                    (d.amount == "0.00" || d.amount.toDoubleOrNull() == 0.0))

        if (paymentSettled(p)) {
            onNothingToPay()
            return@LaunchedEffect
        }

        val tid = p.ticketId
        if (tid != null && p.status.equals("PENDING", ignoreCase = true)) {
            try {
                api.checkout(tid, UUID.randomUUID().toString(), ParkingApiFactory.emptyJsonBody)
                p = api.getPayment(paymentId)
                if (paymentSettled(p)) {
                    onNothingToPay()
                    return@LaunchedEffect
                }
            } catch (e: HttpException) {
                if (e.code() == 409) {
                    val closed = try {
                        api.getTicket(tid).ticket.status == "CLOSED"
                    } catch (_: Exception) {
                        false
                    }
                    if (closed) {
                        onNothingToPay()
                        return@LaunchedEffect
                    }
                }
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }

        payment = p
    }

    Column(Modifier.padding(16.dp)) {
        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {
            Text(UiStrings.Voltar)
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        Text("Pagamento", style = MaterialTheme.typography.titleMedium)
        payment?.let { Text("Valor: R\$ ${it.amount}") }

        Button(
            onClick = onPix,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 12.dp)
                .semantics { contentDescription = UiStrings.B6 },
        ) {
            Text(UiStrings.B6)
        }
        Button(
            onClick = onCard,
            enabled = payment != null,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.B7 },
        ) {
            Text(UiStrings.B7)
        }
        val cashOpen = cash?.open != null
        Button(
            onClick = { showCashConfirm = true },
            enabled = cashOpen,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.B8 },
        ) {
            Text(UiStrings.B8)
        }
        if (!cashOpen) {
            Text(UiStrings.S5, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.outline)
        }
    }

    if (showCashConfirm) {
        AlertDialog(
            onDismissRequest = { showCashConfirm = false },
            confirmButton = {
                Button(
                    onClick = {
                        showCashConfirm = false
                        scope.launch {
                            try {
                                api.payCash(CashPayBody(paymentId))
                                onCashSuccess()
                            } catch (e: HttpException) {
                                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                            } catch (e: Exception) {
                                err = e.message
                            }
                        }
                    },
                ) { Text(UiStrings.Confirmar) }
            },
            dismissButton = {
                Button(onClick = { showCashConfirm = false }) { Text(UiStrings.Voltar) }
            },
            title = { Text(UiStrings.D1) },
        )
    }
}
