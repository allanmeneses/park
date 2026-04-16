package com.estacionamento.parking.ui.loj

import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
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
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.GrantClientBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.plate.PlateOutlinedTextField
import com.estacionamento.parking.plate.PlateValidator
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.util.QrTicketIdParser
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.UUID

@Composable
fun LojGrantScreen(api: ParkingApi, onBack: () -> Unit) {
    val ctx = LocalContext.current
    var plate by remember { mutableStateOf("") }
    var ticketId by remember { mutableStateOf<String?>(null) }
    var hoursText by remember { mutableStateOf("1") }
    var err by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    var restrictHint by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        try {
            val s = api.lojistaGrantSettings()
            if (!s.allowGrantBeforeEntry) restrictHint = UiStrings.S19
        } catch (_: Exception) {
            // fluxo continua sem aviso
        }
    }

    val scanLauncher = rememberLauncherForActivityResult(ScanContract()) { result ->
        err = null
        val raw = result?.contents?.trim().orEmpty()
        if (raw.isEmpty()) return@rememberLauncherForActivityResult
        val id = QrTicketIdParser.firstUuid(raw)
        if (id == null) {
            err = "QR sem ID de cupom válido."
            return@rememberLauncherForActivityResult
        }
        ticketId = id
        plate = ""
    }

    Column(Modifier.padding(16.dp)) {
        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {
            Text(UiStrings.Voltar)
        }
        Text(UiStrings.B27, style = MaterialTheme.typography.titleSmall)
        Text(
            "Informe a placa ou escaneie o QR do cupom. O saldo bonificado do convênio fica separado da carteira comprada do cliente.",
            style = MaterialTheme.typography.bodySmall,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        restrictHint?.let {
            Text(
                it,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.tertiary,
                modifier = Modifier.padding(bottom = 8.dp),
            )
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        ticketId?.let {
            Text("Cupom: $it", style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(bottom = 4.dp))
        }
        PlateOutlinedTextField(
            value = plate,
            onValueChange = {
                plate = it
                ticketId = null
            },
            label = { Text(UiStrings.Placa) },
            enabled = !busy,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        OutlinedTextField(
            value = hoursText,
            onValueChange = { hoursText = it.filter { c -> c.isDigit() }.take(4) },
            label = { Text("Horas") },
            enabled = !busy,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        Button(
            onClick = {
                val options = ScanOptions()
                options.setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                options.setPrompt("Aponte para o QR do cupom")
                scanLauncher.launch(options)
            },
            enabled = !busy,
            modifier = Modifier
                .padding(bottom = 8.dp)
                .semantics { contentDescription = UiStrings.B28 },
        ) {
            Text(UiStrings.B28)
        }
        Button(
            onClick = {
                err = null
                val hRaw = hoursText.trim()
                val h = when {
                    hRaw.isEmpty() -> 1
                    else -> hRaw.toIntOrNull()
                }
                if (h == null || h < 1) {
                    err = "Quantidade de horas inválida."
                    return@Button
                }
                val tid = ticketId?.trim().orEmpty()
                val p = PlateValidator.normalize(plate)
                if (tid.isEmpty() && p.isEmpty()) {
                    err = "Informe a placa ou escaneie o QR do cupom."
                    return@Button
                }
                if (tid.isEmpty() && !PlateValidator.isValidNormalized(p)) {
                    err = UiStrings.E4
                    return@Button
                }
                scope.launch {
                    busy = true
                    try {
                        val body = if (tid.isNotEmpty()) {
                            GrantClientBody(ticketId = tid, hours = h)
                        } else {
                            GrantClientBody(plate = p, hours = h)
                        }
                        val res = api.lojistaGrantClient(UUID.randomUUID().toString(), body)
                        Toast.makeText(
                            ctx,
                            "${UiStrings.T10} ${UiStrings.grantSaldoBonificadoResumo(res.clientBalanceHours, res.lojistaBalanceHours)}",
                            Toast.LENGTH_LONG,
                        ).show()
                        plate = ""
                        ticketId = null
                        hoursText = "1"
                    } catch (e: HttpException) {
                        err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                    } catch (e: Exception) {
                        err = e.message
                    } finally {
                        busy = false
                    }
                }
            },
            enabled = !busy,
            modifier = Modifier.semantics { contentDescription = UiStrings.Confirmar },
        ) {
            Text(UiStrings.Confirmar)
        }
    }
}
