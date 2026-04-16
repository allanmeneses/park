package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ManagerAnalyticsResponse
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.ui.common.ParkingScreenHeader
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun MgrAnalyticsScreen(api: ParkingApi, onBack: () -> Unit) {
    var data by remember { mutableStateOf<ManagerAnalyticsResponse?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var days by remember { mutableIntStateOf(14) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        scope.launch {
            try {
                data = api.managerAnalytics(days.coerceIn(1, 90))
                err = null
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    LaunchedEffect(Unit) { refresh() }

    Column(Modifier.padding(16.dp)) {
        ParkingScreenHeader(title = "Análises e Tendências")
        OutlinedTextField(
            value = days.toString(),
            onValueChange = { days = it.toIntOrNull() ?: 14 },
            label = { Text("Janela (dias)") },
            modifier = Modifier.padding(top = 4.dp),
        )
        Button(onClick = { refresh() }, modifier = Modifier.padding(top = 8.dp)) { Text("Atualizar") }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        data?.let { d ->
            Text("Receita total: ${MgrInsightsFormatter.moneyBrl(d.totals.revenue)}", modifier = Modifier.padding(top = 8.dp))
            Text("Pagamentos: ${d.totals.payments}")
            Text("Check-outs: ${d.totals.checkouts}")
            Text("Horários de pico:", modifier = Modifier.padding(top = 8.dp))
            d.peakHours.forEach { p -> Text("${MgrInsightsFormatter.hourLabel(p.hour)} — ${p.checkouts}") }
            Text("Ganhos por horário:", modifier = Modifier.padding(top = 8.dp))
            LazyColumn(Modifier.padding(top = 4.dp)) {
                items(d.gainsByHour, key = { row -> row.hour }) { row ->
                    Text("${MgrInsightsFormatter.hourLabel(row.hour)} — ${MgrInsightsFormatter.moneyBrl(row.amount)} (${row.payments})")
                }
            }
            Text("Tendência por dia (UTC):", modifier = Modifier.padding(top = 8.dp))
            LazyColumn(Modifier.padding(top = 4.dp)) {
                items(d.trendByDay, key = { row -> row.day }) { row ->
                    Text("${row.day} — ${MgrInsightsFormatter.moneyBrl(row.amount)} (${row.payments})")
                }
            }
        }
        Button(
            onClick = onBack,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.Voltar },
        ) { Text(UiStrings.Voltar) }
    }
}
