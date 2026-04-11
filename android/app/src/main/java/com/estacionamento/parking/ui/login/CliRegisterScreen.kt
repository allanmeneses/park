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
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

private fun normalizeClientPlate(raw: String): String = raw.replace(" ", "").replace("-", "").uppercase()

private fun isClientPlateValid(plate: String): Boolean {
    val mercosul = Regex("^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$")
    val legado = Regex("^[A-Z]{3}[0-9]{4}$")
    return mercosul.matches(plate) || legado.matches(plate)
}

@Composable
fun CliRegisterScreen(
    api: ParkingApi,
    prefs: AuthPrefs,
    onRegistered: (expiresInSeconds: Int) -> Unit,
    onBack: () -> Unit,
) {
    var parkingId by remember { mutableStateOf("") }
    var plate by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var err by remember { mutableStateOf<String?>(null) }
    var parkingErr by remember { mutableStateOf<String?>(null) }
    var plateErr by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    Column(
        Modifier
            .padding(16.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        Text("Cadastro - Cliente", style = MaterialTheme.typography.titleLarge)
        Text(
            "Informe o ID do estacionamento, a placa do veículo, seu e-mail e uma senha para criar a conta.",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        OutlinedTextField(
            value = parkingId,
            onValueChange = {
                parkingId = it
                parkingErr = null
            },
            label = { Text("ID do estacionamento") },
            modifier = Modifier
                .fillMaxWidth()
                .semantics { contentDescription = "ID do estacionamento" },
            singleLine = true,
            isError = parkingErr != null,
            supportingText = parkingErr?.let { { Text(it) } },
        )
        OutlinedTextField(
            value = plate,
            onValueChange = {
                plate = it.uppercase()
                plateErr = null
            },
            label = { Text(UiStrings.Placa) },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.Placa },
            singleLine = true,
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
                parkingErr = null
                plateErr = null
                val pid = parkingId.trim().lowercase()
                parkingId = pid
                if (pid.isBlank()) {
                    parkingErr = UiStrings.E3
                    return@Button
                }
                val plateNorm = normalizeClientPlate(plate)
                plate = plateNorm
                if (!isClientPlateValid(plateNorm)) {
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
                                parkingId = pid,
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
        TextButton(onClick = onBack, modifier = Modifier.padding(top = 8.dp)) {
            Text(UiStrings.Voltar)
        }
    }
}
