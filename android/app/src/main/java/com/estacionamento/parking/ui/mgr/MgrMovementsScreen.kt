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
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ManagerMovementsResponse
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.time.OffsetDateTime

@Composable
fun MgrMovementsScreen(api: ParkingApi, onBack: () -> Unit, onAnalytics: () -> Unit) {
    var data by remember { mutableStateOf<ManagerMovementsResponse?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var kind by remember { mutableStateOf("") }
    var lojistaId by remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    fun refresh() {
        scope.launch {
            try {
                data = api.managerMovements(
                    kind = kind.ifBlank { null },
                    lojistaId = lojistaId.ifBlank { null },
                )
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
        Text("Insights de Movimentações")
        OutlinedTextField(
            value = kind,
            onValueChange = { kind = it },
            label = { Text("Filtro tipo (opcional)") },
            modifier = Modifier.padding(top = 8.dp),
        )
        OutlinedTextField(
            value = lojistaId,
            onValueChange = { lojistaId = it },
            label = { Text("Filtro lojista UUID (opcional)") },
            modifier = Modifier.padding(top = 8.dp),
        )
        Button(onClick = { refresh() }, modifier = Modifier.padding(top = 8.dp)) { Text("Aplicar") }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        data?.let { d ->
            Text("Total ticket: ${MgrInsightsFormatter.moneyBrl(d.insights.totalTicket)}", modifier = Modifier.padding(top = 8.dp))
            Text("Total pacote: ${MgrInsightsFormatter.moneyBrl(d.insights.totalPackage)}")
            Text("Usos lojista: ${d.insights.usagesLojista}")
            Text("Usos cliente: ${d.insights.usagesClient}")
            Text("Registros: ${d.count}")
            LazyColumn(Modifier.padding(top = 8.dp)) {
                items(d.items, key = { row -> row.ref }) { row ->
                    Text(
                        "${row.kind} — ${MgrInsightsFormatter.moneyBrl(row.amount)} — " +
                            "${row.method ?: "—"} — ${splitText(row)} — ${formatUtc(row.at)}",
                        modifier = Modifier.padding(vertical = 6.dp),
                    )
                }
            }
        }
        Button(
            onClick = onAnalytics,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = "Análises" },
        ) { Text("Análises") }
        Button(
            onClick = onBack,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.Voltar },
        ) { Text(UiStrings.Voltar) }
    }
}

private fun formatUtc(raw: String): String {
    return runCatching { OffsetDateTime.parse(raw).toString() }.getOrDefault(raw)
}

private fun splitText(row: com.estacionamento.parking.network.MovementItemDto): String {
    if (row.kind != "TICKET_PAYMENT") return "—"
    return when (row.ticketSplitType) {
        "MIXED" -> "Misto (lojista ${row.hoursLojista}h, cliente ${row.hoursCliente}h, direto ${row.hoursDirect}h)"
        "LOJISTA_ONLY" -> "Lojista (${row.hoursLojista}h)"
        "CLIENT_WALLET_ONLY" -> "Cliente carteira (${row.hoursCliente}h)"
        else -> "Cliente direto"
    }
}
