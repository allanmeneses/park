package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
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

@Composable
fun MgrMovementsScreen(api: ParkingApi, onBack: () -> Unit, onAnalytics: () -> Unit) {
    var data by remember { mutableStateOf<ManagerMovementsResponse?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var fromUtc by remember { mutableStateOf("") }
    var toUtc by remember { mutableStateOf("") }
    var kind by remember { mutableStateOf("") }
    var kindExpanded by remember { mutableStateOf(false) }
    var lojistaId by remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    fun refresh() {
        scope.launch {
            try {
                data = api.managerMovements(
                    from = MgrMovementsSupport.parseInputUtc(fromUtc),
                    to = MgrMovementsSupport.parseInputUtc(toUtc),
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

    fun applyQuick(mode: String) {
        val range = MgrMovementsSupport.quickRange(mode)
        fromUtc = range.fromUtc
        toUtc = range.toUtc
        refresh()
    }

    LaunchedEffect(Unit) { applyQuick("7d") }

    Column(Modifier.padding(16.dp)) {
        Text("Insights de Movimentações", style = MaterialTheme.typography.titleLarge)
        Button(onClick = { applyQuick("24h") }, modifier = Modifier.padding(top = 8.dp)) { Text(UiStrings.S34) }
        Button(onClick = { applyQuick("7d") }, modifier = Modifier.padding(top = 4.dp)) { Text(UiStrings.S35) }
        Button(onClick = { applyQuick("30d") }, modifier = Modifier.padding(top = 4.dp)) { Text(UiStrings.S36) }
        OutlinedTextField(
            value = fromUtc,
            onValueChange = { fromUtc = it },
            label = { Text("De (UTC)") },
            modifier = Modifier.padding(top = 8.dp),
        )
        OutlinedTextField(
            value = toUtc,
            onValueChange = { toUtc = it },
            label = { Text("Até (UTC)") },
            modifier = Modifier.padding(top = 8.dp),
        )
        Text("Tipo", modifier = Modifier.padding(top = 8.dp))
        Button(onClick = { kindExpanded = true }, modifier = Modifier.padding(top = 4.dp)) {
            Text(MgrMovementKinds.labelFor(kind))
        }
        DropdownMenu(expanded = kindExpanded, onDismissRequest = { kindExpanded = false }) {
            MgrMovementKinds.options.forEach { option ->
                DropdownMenuItem(
                    text = { Text(option.label) },
                    onClick = {
                        kind = option.value
                        kindExpanded = false
                    },
                )
            }
        }
        OutlinedTextField(
            value = lojistaId,
            onValueChange = { lojistaId = it },
            label = { Text("Lojista (UUID, opcional)") },
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
                            "${row.method ?: "—"} — ${MgrMovementsSupport.splitText(row)} — ${MgrMovementsSupport.formatUtc(row.at)}",
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
