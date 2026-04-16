package com.estacionamento.parking.ui.login

import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.LoginBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.ui.branding.ParkingLogoMark
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun LoginScreen(
    api: ParkingApi,
    prefs: AuthPrefs,
    onLoggedIn: (expiresInSeconds: Int) -> Unit = { },
    onRegisterClient: () -> Unit = { },
    onRegisterLojista: () -> Unit = { },
) {
    val ctx = LocalContext.current.applicationContext
    var email by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var err by remember { mutableStateOf<String?>(null) }
    var emailErr by remember { mutableStateOf<String?>(null) }
    var passwordErr by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    Column(
        Modifier
            .padding(16.dp)
            .fillMaxWidth(),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        ParkingLogoMark(size = 56.dp)
        Spacer(Modifier.height(12.dp))
        Text(
            "Estacionamento",
            style = MaterialTheme.typography.titleLarge,
            color = MaterialTheme.colorScheme.primary,
        )
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = email,
            onValueChange = {
                email = it
                emailErr = null
            },
            label = { Text("E-mail") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            isError = emailErr != null,
            supportingText = if (emailErr != null) {
                { Text(emailErr!!) }
            } else {
                null
            },
        )
        OutlinedTextField(
            value = password,
            onValueChange = {
                password = it
                passwordErr = null
            },
            label = { Text("Senha") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            isError = passwordErr != null,
            supportingText = if (passwordErr != null) {
                { Text(passwordErr!!) }
            } else {
                null
            },
        )
        err?.let { Text(it, color = androidx.compose.material3.MaterialTheme.colorScheme.error) }
        Button(
            onClick = {
                err = null
                emailErr = null
                passwordErr = null
                if (email.isBlank()) {
                    emailErr = UiStrings.E3
                    return@Button
                }
                if (password.isBlank()) {
                    passwordErr = UiStrings.E3
                    return@Button
                }
                scope.launch {
                    try {
                        val r = api.login(LoginBody(email.trim(), password))
                        prefs.accessToken = r.accessToken
                        prefs.refreshToken = r.refreshToken
                        onLoggedIn(r.expiresIn)
                    } catch (e: HttpException) {
                        val body = e.response()?.errorBody()?.string()
                        val code = ApiErrorMapper.extractCode(body)
                        when {
                            e.code() == 429 || code == "LOGIN_THROTTLED" ->
                                Toast.makeText(ctx, UiStrings.E2, Toast.LENGTH_LONG).show()
                            e.code() == 401 && code == "OPERATOR_BLOCKED" ->
                                Toast.makeText(ctx, UiStrings.E1, Toast.LENGTH_LONG).show()
                            else -> err = ApiErrorMapper.resolve(body)
                        }
                    } catch (e: Exception) {
                        err = e.message ?: "Falha"
                    }
                }
            },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.B1 },
        ) {
            Text(UiStrings.B1)
        }
        TextButton(
            onClick = onRegisterClient,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.B34 },
        ) {
            Text(UiStrings.B34)
        }
        TextButton(
            onClick = onRegisterLojista,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.B25 },
        ) {
            Text(UiStrings.B25)
        }
    }
}
