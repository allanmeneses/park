package com.estacionamento.parking.ui.cli

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
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
import com.estacionamento.parking.history.WalletHistoryFormatter
import com.estacionamento.parking.network.HistoryItemDto
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun CliHistoryScreen(api: ParkingApi, onBack: () -> Unit) {
    var items by remember { mutableStateOf<List<HistoryItemDto>>(emptyList()) }
    var nextCursor by remember { mutableStateOf<String?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var state by remember { mutableStateOf("loading") }
    val scope = rememberCoroutineScope()

    fun load(cursor: String? = null) {
        scope.launch {
            state = "loading"
            err = null
            try {
                val response = api.clientHistory(limit = 50, cursor = cursor)
                items = if (cursor.isNullOrBlank()) response.items else items + response.items
                nextCursor = response.nextCursor
                state = "ready"
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                state = "error"
            } catch (e: Exception) {
                err = e.message
                state = "error"
            }
        }
    }

    LaunchedEffect(Unit) { load() }

    Column(Modifier.padding(16.dp)) {
        Text("Histórico", style = MaterialTheme.typography.titleLarge)
        if (state == "loading" && items.isEmpty()) {
            Text(UiStrings.S32, modifier = Modifier.padding(top = 8.dp))
        } else if (state == "error" && err != null) {
            Text(err!!, color = MaterialTheme.colorScheme.error)
        } else if (items.isEmpty()) {
            Text("Sem movimentos.")
        } else {
            LazyColumn(Modifier.padding(top = 8.dp)) {
                items(items, key = { row -> row.id }) { row ->
                    Text(
                        "${WalletHistoryFormatter.kindLabel(row.kind)} — " +
                            WalletHistoryFormatter.formatDeltaHours(row.kind, row.deltaHours) +
                            if (row.kind == "PURCHASE") {
                                " — ${WalletHistoryFormatter.formatAmountBrl(row.amount)}"
                            } else {
                                ""
                            } +
                            " — ${WalletHistoryFormatter.formatWhen(row.createdAt)}",
                        modifier = Modifier.padding(vertical = 8.dp),
                    )
                }
            }
        }
        if (!nextCursor.isNullOrBlank() && state == "ready") {
            Button(
                onClick = { load(nextCursor) },
                modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.S31 },
            ) {
                Text(UiStrings.S31)
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
