package com.estacionamento.parking.ui.loj

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
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
import com.estacionamento.parking.history.WalletHistoryFormatter
import com.estacionamento.parking.network.HistoryItemDto
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.Locale

@Composable
fun LojHistoryScreen(api: ParkingApi, onBack: () -> Unit) {
    var items by remember { mutableStateOf<List<HistoryItemDto>>(emptyList()) }
    var err by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        scope.launch {
            try {
                items = api.lojistaHistory().items
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    Column(Modifier.padding(16.dp)) {
        Text("Histórico")
        err?.let { Text(it, color = androidx.compose.material3.MaterialTheme.colorScheme.error) }
        if (items.isEmpty() && err == null) {
            Text("Sem movimentos.")
        } else {
            LazyColumn {
                items(items, key = { row -> row.id }) { row ->
                    val amt = if (row.kind == "PURCHASE") {
                        val n = row.amount.replace(',', '.').toDoubleOrNull() ?: 0.0
                        " — ${String.format(Locale.forLanguageTag("pt-BR"), "R\$ %.2f", n)}"
                    } else ""
                    Text(
                        "${WalletHistoryFormatter.kindLabel(row.kind)} — " +
                            WalletHistoryFormatter.formatDeltaHours(row.kind, row.deltaHours) +
                            amt + " — ${row.createdAt}",
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
