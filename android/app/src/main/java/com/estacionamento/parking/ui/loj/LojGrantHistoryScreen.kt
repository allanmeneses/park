package com.estacionamento.parking.ui.loj

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
import com.estacionamento.parking.network.LojistaGrantHistoryItemDto
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.plate.PlateOutlinedTextField
import com.estacionamento.parking.plate.PlateValidator
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.util.Locale

private val ymdPattern = Regex("^\\d{4}-\\d{2}-\\d{2}$")

private fun dayStartUtc(isoDay: String): String? {
    val t = isoDay.trim()
    if (t.isEmpty()) return null
    if (!ymdPattern.matches(t)) return null
    return "${t}T00:00:00.000Z"
}

private fun dayEndUtc(isoDay: String): String? {
    val t = isoDay.trim()
    if (t.isEmpty()) return null
    if (!ymdPattern.matches(t)) return null
    return "${t}T23:59:59.999Z"
}

private fun formatGrantRow(iso: String): String =
    runCatching {
        val odt = OffsetDateTime.parse(iso)
        val utc = odt.atZoneSameInstant(ZoneOffset.UTC)
        val fmt = DateTimeFormatter.ofPattern("dd/MM/yyyy HH:mm", Locale.forLanguageTag("pt-BR"))
        utc.format(fmt) + " UTC"
    }.getOrDefault(iso)

private fun grantModeLabel(mode: String): String =
    if (mode == "ON_SITE") "com veiculo no patio" else "antecipado"

@Composable
fun LojGrantHistoryScreen(api: ParkingApi, onBack: () -> Unit) {
    var items by remember { mutableStateOf<List<LojistaGrantHistoryItemDto>>(emptyList()) }
    var err by remember { mutableStateOf<String?>(null) }
    var fromDay by remember { mutableStateOf("") }
    var toDay by remember { mutableStateOf("") }
    var plateFilter by remember { mutableStateOf("") }
    val scope = rememberCoroutineScope()

    fun refresh() {
        scope.launch {
            try {
                val from = dayStartUtc(fromDay)
                val to = dayEndUtc(toDay)
                val plate = PlateValidator.normalize(plateFilter).ifBlank { null }
                items = api.lojistaGrantHistory(
                    from = from,
                    to = to,
                    plate = plate,
                    limit = 100,
                ).items
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
        Text(UiStrings.B29, style = MaterialTheme.typography.titleSmall)
        Text(
            "Filtros opcionais (datas em UTC, formato AAAA-MM-DD).",
            style = MaterialTheme.typography.bodySmall,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        OutlinedTextField(
            value = fromDay,
            onValueChange = { fromDay = it },
            label = { Text("De (AAAA-MM-DD)") },
            modifier = Modifier.padding(bottom = 4.dp),
        )
        OutlinedTextField(
            value = toDay,
            onValueChange = { toDay = it },
            label = { Text("Até (AAAA-MM-DD)") },
            modifier = Modifier.padding(bottom = 4.dp),
        )
        PlateOutlinedTextField(
            value = plateFilter,
            onValueChange = { plateFilter = it },
            label = { Text("Placa (opcional)") },
            modifier = Modifier.padding(bottom = 8.dp),
        )
        Button(
            onClick = { refresh() },
            modifier = Modifier
                .padding(bottom = 8.dp)
                .semantics { contentDescription = "Aplicar filtros" },
        ) {
            Text("Aplicar filtros")
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        if (items.isEmpty() && err == null) {
            Text("Nenhum registo.")
        } else {
            LazyColumn(Modifier.padding(top = 4.dp)) {
                items(items, key = { it.id }) { row ->
                    Text(
                        "${formatGrantRow(row.createdAt)} - placa ${row.plate} - ${row.hours} h - modo: ${grantModeLabel(row.grantMode)}",
                        modifier = Modifier.padding(vertical = 8.dp),
                    )
                }
            }
        }
        Button(
            onClick = onBack,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.Voltar },
        ) {
            Text(UiStrings.Voltar)
        }
    }
}
