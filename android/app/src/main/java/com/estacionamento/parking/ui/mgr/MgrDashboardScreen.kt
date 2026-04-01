package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
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
import com.estacionamento.parking.network.DashboardResponse
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.Locale

@Composable
fun MgrDashboardScreen(
    api: ParkingApi,
    onInsights: () -> Unit,
    onAnalytics: () -> Unit,
    onCash: () -> Unit,
    onSettings: () -> Unit,
    onOperacao: () -> Unit,
    onLogout: () -> Unit,
) {
    var data by remember { mutableStateOf<DashboardResponse?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        scope.launch {
            try {
                data = api.dashboard()
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    Column(Modifier.padding(16.dp)) {
        Text("Painel")
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        data?.let { d ->
            val uso = d.usoConvenio?.let { "${String.format(Locale.forLanguageTag("pt-BR"), "%.1f", it * 100)}%" } ?: "—"
            Text("Faturamento (hoje): ${String.format(Locale.forLanguageTag("pt-BR"), "R\$ %.2f", d.faturamento)}")
            Text("Ocupação: ${String.format(Locale.forLanguageTag("pt-BR"), "%.1f", d.ocupacao * 100)}%")
            Text("Check-outs hoje: ${d.ticketsDia}")
            Text("Uso convênio: $uso")
        }
        Button(
            onClick = onInsights,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.B22 },
        ) {
            Text(UiStrings.B22)
        }
        Button(
            onClick = onAnalytics,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B23 },
        ) {
            Text(UiStrings.B23)
        }
        Button(
            onClick = onCash,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B12 },
        ) {
            Text(UiStrings.B12)
        }
        Button(
            onClick = onSettings,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B13 },
        ) {
            Text(UiStrings.B13)
        }
        Button(
            onClick = onOperacao,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B21 },
        ) {
            Text(UiStrings.B21)
        }
        Button(
            onClick = onLogout,
            modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.Sair },
        ) {
            Text(UiStrings.Sair)
        }
    }
}
