package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
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
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.CashCloseBody
import com.estacionamento.parking.network.CashGetResponse
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun MgrCashScreen(
    api: ParkingApi,
    onBack: () -> Unit,
) {
    var cash by remember { mutableStateOf<CashGetResponse?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var actualStr by remember { mutableStateOf("") }
    var showDivAlert by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    fun load() {
        scope.launch {
            try {
                cash = api.cashStatus()
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    LaunchedEffect(Unit) { load() }

    Column(Modifier.padding(16.dp)) {
        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {
            Text(UiStrings.Voltar)
        }
        Text(UiStrings.S11, style = MaterialTheme.typography.titleMedium)
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }

        val open = cash?.open
        if (open == null) {
            Button(
                onClick = {
                    scope.launch {
                        try {
                            api.cashOpen()
                            load()
                        } catch (e: HttpException) {
                            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                        } catch (e: Exception) {
                            err = e.message
                        }
                    }
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp)
                    .semantics { contentDescription = UiStrings.B14 },
            ) {
                Text(UiStrings.B14)
            }
            Button(
                onClick = { },
                enabled = false,
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            ) {
                Text(UiStrings.B15)
            }
        } else {
            Text("Esperado: R\$ ${open.expectedAmount}")
            OutlinedTextField(
                value = actualStr,
                onValueChange = { actualStr = it },
                label = { Text("Valor contado") },
                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                singleLine = true,
            )
            Button(
                onClick = {
                    val v = actualStr.replace(",", ".").toDoubleOrNull()
                    if (v == null) {
                        err = ApiErrorMapper.messageForCode("VALIDATION_ERROR")
                            ?: "Verifique os dados informados."
                        return@Button
                    }
                    scope.launch {
                        try {
                            val r = api.cashClose(CashCloseBody(open.sessionId, v))
                            if (r.alert) showDivAlert = true
                            load()
                            actualStr = ""
                        } catch (e: HttpException) {
                            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                        } catch (e: Exception) {
                            err = e.message
                        }
                    }
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp)
                    .semantics { contentDescription = UiStrings.B15 },
            ) {
                Text(UiStrings.B15)
            }
        }
    }

    if (showDivAlert) {
        AlertDialog(
            onDismissRequest = { showDivAlert = false },
            confirmButton = { Button(onClick = { showDivAlert = false }) { Text(UiStrings.Confirmar) } },
            title = { Text(UiStrings.T6) },
        )
    }
}
