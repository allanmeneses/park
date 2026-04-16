package com.estacionamento.parking.ui.login

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.RegisterClientBody
import com.estacionamento.parking.plate.PlateOutlinedTextField
import com.estacionamento.parking.plate.PlateValidator
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.ui.common.ParkingScreenHeader
import kotlinx.coroutines.launch
import retrofit2.HttpException

/** UUID v4 (SPEC / alinhado à Web `isValidParkingUuid`). */
private fun isParkingUuid(id: String): Boolean {
    val s = id.trim().lowercase()
    return Regex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$").matches(s)
}

@Composable
fun CliRegisterScreen(
    api: ParkingApi,
    prefs: AuthPrefs,
    initialParkingId: String? = null,
    onRegistered: (expiresInSeconds: Int) -> Unit,
    onBack: () -> Unit,
) {
    val lockedParking = remember(initialParkingId) {
        initialParkingId?.trim()?.lowercase()?.takeIf { isParkingUuid(it) }
    }
    val invalidLink = remember(initialParkingId) {
        val raw = initialParkingId?.trim().orEmpty()
        raw.isNotEmpty() && lockedParking == null
    }
    var plate by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var err by remember { mutableStateOf<String?>(null) }
    var plateErr by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    Column(
        Modifier
            .padding(16.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        ParkingScreenHeader(title = "Cadastro - Cliente", showMark = true)

        when {
            invalidLink -> {
                Text(
                    "Este link de cadastro não é válido. Peça ao estacionamento um novo link (por exemplo QR code, WhatsApp ou e-mail).",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.padding(bottom = 8.dp),
                )
            }
            lockedParking == null -> {
                Text(
                    "Para criar a sua conta precisa do link de cadastro que o estacionamento lhe enviar. Esse link já identifica onde estaciona.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(bottom = 8.dp),
                )
                Text(
                    "Se não tiver o link, peça na receção ou ao responsável pelo estacionamento.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(bottom = 8.dp),
                )
            }
            else -> {
                val parkingId = requireNotNull(lockedParking)
                Text(
                    "O estacionamento já foi identificado pelo link. Informe a placa, o e-mail e a senha.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.padding(bottom = 8.dp),
                )
                PlateOutlinedTextField(
                    value = plate,
                    onValueChange = {
                        plate = it
                        plateErr = null
                    },
                    label = { Text(UiStrings.Placa) },
                    modifier = Modifier
                        .fillMaxWidth()
                        .semantics { contentDescription = UiStrings.Placa },
                    isError = plateErr != null,
                    supportingText = plateErr?.let { { Text(it) } },
                )
                OutlinedTextField(
                    value = email,
                    onValueChange = { email = it },
                    label = { Text("E-mail") },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp)
                        .semantics { contentDescription = "E-mail" },
                    singleLine = true,
                )
                OutlinedTextField(
                    value = password,
                    onValueChange = { password = it },
                    label = { Text("Senha") },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp)
                        .semantics { contentDescription = "Senha" },
                    singleLine = true,
                )
                err?.let {
                    Text(
                        it,
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.padding(top = 8.dp),
                    )
                }
                Button(
                    onClick = {
                        err = null
                        plateErr = null
                        val plateNorm = PlateValidator.normalize(plate)
                        plate = plateNorm
                        if (!PlateValidator.isValidNormalized(plateNorm)) {
                            plateErr = UiStrings.E4
                            return@Button
                        }
                        if (email.isBlank() || password.isBlank()) {
                            err = UiStrings.E3
                            return@Button
                        }
                        scope.launch {
                            try {
                                val r = api.registerClient(
                                    RegisterClientBody(
                                        parkingId = parkingId,
                                        plate = plateNorm,
                                        email = email.trim(),
                                        password = password,
                                    ),
                                )
                                prefs.accessToken = r.accessToken
                                prefs.refreshToken = r.refreshToken
                                onRegistered(r.expiresIn)
                            } catch (e: HttpException) {
                                val body = e.response()?.errorBody()?.string()
                                err = ApiErrorMapper.resolve(body)
                            } catch (e: Exception) {
                                err = e.message ?: "Falha"
                            }
                        }
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 12.dp)
                        .semantics { contentDescription = UiStrings.B24 },
                ) {
                    Text(UiStrings.B24)
                }
            }
        }

        TextButton(onClick = onBack, modifier = Modifier.padding(top = 8.dp)) {
            Text(UiStrings.Voltar)
        }
    }
}
