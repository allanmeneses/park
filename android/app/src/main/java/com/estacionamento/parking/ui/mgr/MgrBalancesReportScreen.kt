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
import com.estacionamento.parking.network.ManagerBalancesReportResponse
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.plate.PlateOutlinedTextField
import com.estacionamento.parking.plate.PlateValidator
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun MgrBalancesReportScreen(api: ParkingApi, onBack: () -> Unit) {
    var plateFilter by remember { mutableStateOf("") }
    var data by remember { mutableStateOf<ManagerBalancesReportResponse?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var loading by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    fun refresh() {
        scope.launch {
            loading = true
            err = null
            try {
                val p = PlateValidator.normalize(plateFilter).takeIf { it.isNotEmpty() }
                data = api.managerBalancesReport(plate = p)
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                data = null
            } catch (e: Exception) {
                err = e.message
                data = null
            } finally {
                loading = false
            }
        }
    }

    LaunchedEffect(Unit) { refresh() }

    Column(Modifier.padding(16.dp)) {
        Text(
            UiStrings.B32,
            modifier = Modifier.semantics { contentDescription = UiStrings.B32 },
        )
        Text(
            "Convênio por lojista, bonificação disponível por placa e carteira comprada.",
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(top = 4.dp),
        )
        PlateOutlinedTextField(
            value = plateFilter,
            onValueChange = { plateFilter = it },
            label = { Text("Filtrar placa") },
            modifier = Modifier
                .padding(top = 12.dp)
                .semantics { contentDescription = "Filtrar placa" },
        )
        Button(
            onClick = { refresh() },
            enabled = !loading,
            modifier = Modifier
                .padding(top = 8.dp)
                .semantics { contentDescription = "Atualizar relatório de saldos" },
        ) {
            Text("Atualizar")
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp)) }
        if (loading && data == null) {
            Text("Carregando…", modifier = Modifier.padding(top = 8.dp))
        }
        data?.let { d ->
            Text("Lojistas — saldo convênio (h)", modifier = Modifier.padding(top = 16.dp))
            if (d.lojistas.isEmpty()) {
                Text("Nenhum lojista com carteira registada.", style = MaterialTheme.typography.bodySmall)
            } else {
                LazyColumn(Modifier.padding(top = 4.dp)) {
                    items(d.lojistas, key = { it.lojistaId }) { row ->
                        Text("${row.lojistaName ?: "—"} — ${row.balanceHours} h")
                    }
                }
            }
            Text("Placas — bonificação lojista disponível (h)", modifier = Modifier.padding(top = 16.dp))
            if (d.lojistaBonificadoPlates.isEmpty()) {
                Text(
                    "Nenhuma placa com bonificação disponível (com o filtro atual).",
                    style = MaterialTheme.typography.bodySmall,
                )
            } else {
                LazyColumn(Modifier.padding(top = 4.dp)) {
                    items(d.lojistaBonificadoPlates, key = { it.plate }) { row ->
                        Text("${row.plate} — ${row.balanceHours} h")
                    }
                }
            }
            Text("Clientes — crédito comprado por placa (h)", modifier = Modifier.padding(top = 16.dp))
            if (d.clientPlates.isEmpty()) {
                Text("Nenhum cliente com o filtro atual.", style = MaterialTheme.typography.bodySmall)
            } else {
                LazyColumn(Modifier.padding(top = 4.dp)) {
                    items(d.clientPlates, key = { it.plate }) { row ->
                        val exp = row.expirationDate?.let { x -> " (validade $x)" } ?: ""
                        Text("${row.plate} — ${row.balanceHours} h$exp")
                    }
                }
            }
        }
        Button(
            onClick = onBack,
            modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.Voltar },
        ) { Text(UiStrings.Voltar) }
    }
}
