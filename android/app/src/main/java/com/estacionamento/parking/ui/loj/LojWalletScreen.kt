package com.estacionamento.parking.ui.loj

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.LojistaGrantSettingsBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun LojWalletScreen(
    api: ParkingApi,
    onHistory: () -> Unit,
    onBuy: () -> Unit,
    onGrant: () -> Unit,
    onGrantHistory: () -> Unit,
    onLogout: () -> Unit,
) {
    var bal by remember { mutableStateOf<Int?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var grantErr by remember { mutableStateOf<String?>(null) }
    var grantSaveErr by remember { mutableStateOf<String?>(null) }
    var grantPrefsLoaded by remember { mutableStateOf(false) }
    var restrictToLot by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        coroutineScope {
            launch {
                try {
                    bal = api.lojistaWallet().balanceHours
                } catch (e: HttpException) {
                    err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                } catch (e: Exception) {
                    err = e.message
                }
            }
            launch {
                try {
                    val gs = api.lojistaGrantSettings()
                    restrictToLot = !gs.allowGrantBeforeEntry
                    grantPrefsLoaded = true
                } catch (e: HttpException) {
                    grantErr = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                } catch (e: Exception) {
                    grantErr = e.message ?: "Erro."
                }
            }
        }
    }

    Column(Modifier.padding(16.dp)) {
        Text("Carteira de convênio")
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        bal?.let { Text("Saldo: $it horas") }

        if (grantErr != null) {
            Text(grantErr!!, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp))
        } else if (grantPrefsLoaded) {
            Text(
                "Bonificação a clientes",
                style = MaterialTheme.typography.titleSmall,
                modifier = Modifier.padding(top = 12.dp),
            )
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.padding(top = 4.dp),
            ) {
                Switch(
                    checked = restrictToLot,
                    onCheckedChange = { newVal ->
                        val previous = restrictToLot
                        restrictToLot = newVal
                        scope.launch {
                            try {
                                api.lojistaPutGrantSettings(
                                    LojistaGrantSettingsBody(allowGrantBeforeEntry = !newVal),
                                )
                                grantSaveErr = null
                            } catch (e: HttpException) {
                                restrictToLot = previous
                                grantSaveErr = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                            } catch (e: Exception) {
                                restrictToLot = previous
                                grantSaveErr = e.message
                            }
                        }
                    },
                    modifier = Modifier.semantics { contentDescription = UiStrings.B30 },
                )
                Text(UiStrings.B30, modifier = Modifier.padding(start = 8.dp))
            }
            Text(
                if (restrictToLot) UiStrings.S18 else UiStrings.S17,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(top = 4.dp),
            )
            grantSaveErr?.let {
                Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 4.dp))
            }
        }

        Button(
            onClick = onBuy,
            modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.B16 },
        ) {
            Text(UiStrings.B16)
        }
        Button(
            onClick = onGrant,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B27 },
        ) {
            Text(UiStrings.B27)
        }
        Button(
            onClick = onGrantHistory,
            modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B29 },
        ) {
            Text(UiStrings.B29)
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
