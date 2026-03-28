package com.estacionamento.parking.ui.loj

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
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
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun LojWalletScreen(
    api: ParkingApi,
    onHistory: () -> Unit,
    onBuy: () -> Unit,
    onLogout: () -> Unit,
) {
    var bal by remember { mutableStateOf<Int?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        scope.launch {
            try {
                bal = api.lojistaWallet().balanceHours
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    Column(Modifier.padding(16.dp)) {
        Text("Carteira de convênio")
        err?.let { Text(it, color = androidx.compose.material3.MaterialTheme.colorScheme.error) }
        bal?.let { Text("Saldo: $it horas") }
        Button(
            onClick = onBuy,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.B16 },
        ) {
            Text(UiStrings.B16)
        }
        Button(
            onClick = onHistory,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B17 },
        ) {
            Text(UiStrings.B17)
        }
        Button(
            onClick = onLogout,
            modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.Sair },
        ) {
            Text(UiStrings.Sair)
        }
    }
}
