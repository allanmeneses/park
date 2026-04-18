package com.estacionamento.parking.ui.common

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.ParkingApiFactory
import com.estacionamento.parking.network.PixPayBody
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.time.Instant
import java.time.format.DateTimeParseException
import java.util.UUID

@Composable
fun PayPixScreen(
    api: ParkingApi,
    paymentId: String,
    paidToast: String,
    onPaid: () -> Unit,
    onBack: () -> Unit,
    onFailedBack: () -> Unit,
) {
    val ctx = LocalContext.current
    val scope = rememberCoroutineScope()
    var qr by remember { mutableStateOf<String?>(null) }
    var expiresAtIso by remember { mutableStateOf<String?>(null) }
    var bitmap by remember { mutableStateOf<ImageBitmap?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var expiredUi by remember { mutableStateOf(false) }
    var timeoutUi by remember { mutableStateOf(false) }
    var remainingSec by remember { mutableLongStateOf(0L) }
    var reloadToken by remember { mutableStateOf(0) }

    fun loadPix() {
        scope.launch {
            err = null
            expiredUi = false
            timeoutUi = false
            try {
                val r = api.payPix(PixPayBody(paymentId))
                qr = r.qrCode
                expiresAtIso = r.expiresAt
                bitmap = QrBitmap.encode(r.qrCode, 480)
                reloadToken++
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    LaunchedEffect(paymentId) { loadPix() }

    /** Ticket no pátio: recalcula checkout periodicamente para o valor acompanhar o tempo (desistência / troca de meio). */
    LaunchedEffect(paymentId) {
        var lastAmount: String? = null
        while (isActive) {
            delay(45_000)
            try {
                val p0 = api.getPayment(paymentId)
                val tid = p0.ticketId ?: continue
                if (lastAmount == null) lastAmount = p0.amount
                try {
                    api.checkout(tid, UUID.randomUUID().toString(), ParkingApiFactory.emptyJsonBody)
                } catch (e: HttpException) {
                    if (e.code() != 409) continue
                }
                val p1 = api.getPayment(paymentId)
                if (p1.amount != lastAmount) {
                    lastAmount = p1.amount
                    loadPix()
                }
            } catch (_: Exception) {
            }
        }
    }

    LaunchedEffect(reloadToken, expiresAtIso) {
        val expStr = expiresAtIso ?: return@LaunchedEffect
        while (isActive && !timeoutUi) {
            val exp = parseInstant(expStr) ?: break
            val left = (exp.toEpochMilli() - System.currentTimeMillis()) / 1000
            remainingSec = left.coerceAtLeast(0)
            if (left <= 0) {
                expiredUi = true
                break
            }
            delay(1000)
        }
    }

    LaunchedEffect(paymentId, reloadToken) {
        val deadline = System.currentTimeMillis() + 900_000
        while (isActive) {
            if (System.currentTimeMillis() > deadline) {
                timeoutUi = true
                break
            }
            delay(2000)
            try {
                val p = api.getPayment(paymentId)
                when (p.status.trim().uppercase()) {
                    "PAID" -> {
                        Toast.makeText(ctx, paidToast, Toast.LENGTH_SHORT).show()
                        onPaid()
                        return@LaunchedEffect
                    }
                    "EXPIRED" -> expiredUi = true
                    "FAILED" -> {
                        Toast.makeText(ctx, UiStrings.E7, Toast.LENGTH_LONG).show()
                        onFailedBack()
                        return@LaunchedEffect
                    }
                }
            } catch (_: Exception) { }
        }
    }

    Column(Modifier.padding(16.dp)) {
        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {
            Text(UiStrings.Voltar)
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        when {
            timeoutUi -> {
                Text(UiStrings.S8)
                Button(
                    onClick = { timeoutUi = false; loadPix() },
                    modifier = Modifier.semantics { contentDescription = UiStrings.B10 },
                ) { Text(UiStrings.B10) }
            }
            expiredUi -> {
                Text(UiStrings.S7)
                Button(
                    onClick = { expiredUi = false; loadPix() },
                    modifier = Modifier.semantics { contentDescription = UiStrings.B10 },
                ) { Text(UiStrings.B10) }
            }
            else -> {
                bitmap?.let {
                    Image(it, contentDescription = "", modifier = Modifier.size(280.dp))
                }
                if (remainingSec > 0 && !expiredUi) {
                    Text("Expira em: ${remainingSec}s")
                }
                qr?.let { payload ->
                    Button(
                        onClick = {
                            val cm = ctx.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            cm.setPrimaryClip(ClipData.newPlainText("pix", payload))
                            Toast.makeText(ctx, UiStrings.T5, Toast.LENGTH_SHORT).show()
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 8.dp)
                            .semantics { contentDescription = UiStrings.B9 },
                    ) {
                        Text(UiStrings.B9)
                    }
                    Button(
                        onClick = { loadPix() },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(top = 4.dp)
                            .semantics { contentDescription = UiStrings.B10 },
                    ) {
                        Text(UiStrings.B10)
                    }
                }
            }
        }
    }
}

private fun parseInstant(iso: String): Instant? =
    try {
        Instant.parse(iso)
    } catch (_: DateTimeParseException) {
        null
    }
