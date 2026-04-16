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
import com.estacionamento.parking.network.RegisterLojistaBody
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.ui.common.ParkingScreenHeader
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun LojRegisterScreen(
    api: ParkingApi,
    prefs: AuthPrefs,
    onRegistered: (expiresInSeconds: Int) -> Unit,
    onBack: () -> Unit,
) {
    var merchant by remember { mutableStateOf("") }
    var activation by remember { mutableStateOf("") }
    var name by remember { mutableStateOf("") }
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var err by remember { mutableStateOf<String?>(null) }
    var merchantErr by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    Column(
        Modifier
            .padding(16.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        ParkingScreenHeader(
            title = "Cadastro — Lojista",
            showMark = true,
            subtitle = "Use o código de 10 caracteres e o código de ativação fornecidos pelo gestor.",
        )
        OutlinedTextField(
            value = merchant,
            onValueChange = {
                merchant = it.uppercase()
                merchantErr = null
            },
            label = { Text("Código do lojista") },
            modifier = Modifier
                .fillMaxWidth()
                .semantics { contentDescription = "Código do lojista" },
            singleLine = true,
            isError = merchantErr != null,
            supportingText = merchantErr?.let { { Text(it) } },
        )
        OutlinedTextField(
            value = activation,
            onValueChange = { activation = it },
            label = { Text("Código de ativação") },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = "Código de ativação" },
            singleLine = true,
        )
        OutlinedTextField(
            value = name,
            onValueChange = { name = it },
            label = { Text("Nome da loja") },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = "Nome da loja" },
            singleLine = true,
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
                val mc = merchant.trim().uppercase()
                merchant = mc
                if (mc.length != 10) {
                    merchantErr = UiStrings.E9
                    return@Button
                }
                if (activation.isBlank() || name.isBlank() || email.isBlank() || password.isBlank()) {
                    err = UiStrings.E3
                    return@Button
                }
                scope.launch {
                    try {
                        val r = api.registerLojista(
                            RegisterLojistaBody(
                                merchantCode = mc,
                                activationCode = activation.trim(),
                                email = email.trim(),
                                password = password,
                                name = name.trim(),
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
