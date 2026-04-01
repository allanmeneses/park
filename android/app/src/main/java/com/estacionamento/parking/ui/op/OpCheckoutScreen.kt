package com.estacionamento.parking.ui.op

import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.ParkingApiFactory
import com.estacionamento.parking.offline.OfflineQueueItem
import com.estacionamento.parking.offline.OfflineQueueStore
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import retrofit2.HttpException
import java.util.UUID

@Composable
fun OpCheckoutScreen(
    api: ParkingApi,
    offlineStore: OfflineQueueStore,
    isOnline: () -> Boolean,
    ticketId: String,
    onZeroAmount: () -> Unit,
    onNeedPayment: (paymentId: String) -> Unit,
    onInvalidState: () -> Unit,
    onBack: () -> Unit,
    onCheckoutQueued: () -> Unit,
) {
    val ctx = LocalContext.current.applicationContext
    var loading by remember { mutableStateOf(true) }
    var err by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(ticketId) {
        loading = true
        err = null
        if (!isOnline()) {
            offlineStore.enqueue(
                OfflineQueueItem(
                    idLocal = UUID.randomUUID().toString(),
                    path = "tickets/$ticketId/checkout",
                    idempotencyKey = UUID.randomUUID().toString(),
                    bodyJson = "{}",
                    createdAtEpoch = System.currentTimeMillis(),
                ),
            )
            Toast.makeText(ctx, UiStrings.TQueueSync, Toast.LENGTH_LONG).show()
            loading = false
            onCheckoutQueued()
            return@LaunchedEffect
        }
        withContext(Dispatchers.IO) {
            try {
                val r = api.checkout(ticketId, UUID.randomUUID().toString(), ParkingApiFactory.emptyJsonBody)
                val zero = r.amount == "0.00" || r.amount.toDoubleOrNull() == 0.0
                withContext(Dispatchers.Main) {
                    loading = false
                    if (zero) {
                        val parts = mutableListOf(UiStrings.T3)
                        if (r.hoursTotal > 0) parts += "Total faturável: ${r.hoursTotal} h."
                        if (r.hoursCliente > 0) parts += "Carteira cliente: −${r.hoursCliente} h."
                        if (r.hoursLojista > 0) parts += "Convênio lojista: −${r.hoursLojista} h."
                        Toast.makeText(ctx, parts.joinToString(" "), Toast.LENGTH_LONG).show()
                        onZeroAmount()
                    } else {
                        onNeedPayment(r.paymentId)
                    }
                }
            } catch (e: HttpException) {
                val code = e.response()?.errorBody()?.string()
                val resolved = ApiErrorMapper.resolve(code)
                withContext(Dispatchers.Main) {
                    loading = false
                    if (e.code() == 409 && code?.contains("INVALID_TICKET_STATE") == true) {
                        onInvalidState()
                    } else {
                        err = resolved
                    }
                }
            } catch (e: Exception) {
                withContext(Dispatchers.Main) {
                    loading = false
                    err = e.message
                }
            }
        }
    }

    Column(Modifier.padding(16.dp)) {
        when {
            loading -> {
                Text("Checkout…")
                CircularProgressIndicator(Modifier.padding(top = 8.dp))
            }
            err != null -> {
                Text(err!!, color = MaterialTheme.colorScheme.error)
                Button(
                    onClick = onBack,
                    modifier = Modifier
                        .padding(top = 8.dp)
                        .semantics { contentDescription = UiStrings.Voltar },
                ) {
                    Text(UiStrings.Voltar)
                }
            }
            else -> Spacer(Modifier.height(0.dp))
        }
    }
}
