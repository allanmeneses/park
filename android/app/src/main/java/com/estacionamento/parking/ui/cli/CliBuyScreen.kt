package com.estacionamento.parking.ui.cli

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
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
import android.widget.Toast
import androidx.compose.ui.platform.LocalContext
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ClientBuyBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.RechargePackageDto
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.UUID

@Composable
fun CliBuyScreen(
    api: ParkingApi,
    onBack: () -> Unit,
    onPayPix: (paymentId: String) -> Unit,
    onCreditDone: () -> Unit,
) {
    val ctx = LocalContext.current
    var items by remember { mutableStateOf<List<RechargePackageDto>>(emptyList()) }
    var err by remember { mutableStateOf<String?>(null) }
    var pick by remember { mutableStateOf<RechargePackageDto?>(null) }
    var showCreditConfirm by remember { mutableStateOf(false) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        try {
            items = api.rechargePackages("CLIENT").items
        } catch (e: HttpException) {
            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
        } catch (e: Exception) {
            err = e.message
        }
    }

    Column(Modifier.padding(16.dp)) {
        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {
            Text(UiStrings.Voltar)
        }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        LazyColumn {
            items(items, key = { it.id }) { p ->
                Button(
                    onClick = { pick = p },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 4.dp)
                        .semantics { contentDescription = UiStrings.B18 },
                ) {
                    Text("${p.hours} h — R\$ ${p.price}")
                }
            }
        }
    }

    pick?.let { pkg ->
        AlertDialog(
            onDismissRequest = { pick = null },
            title = { Text("Pacote ${pkg.hours} h") },
            text = {
                Column {
                    Button(
                        onClick = { showCreditConfirm = true },
                        modifier = Modifier.fillMaxWidth().semantics { contentDescription = UiStrings.Credito },
                    ) { Text(UiStrings.Credito) }
                    Button(
                        onClick = {
                            scope.launch {
                                try {
                                    val r = api.clientBuy(
                                        UUID.randomUUID().toString(),
                                        ClientBuyBody(pkg.id, "PIX"),
                                    )
                                    val pid = r.paymentId
                                    if (pid != null) {
                                        pick = null
                                        onPayPix(pid)
                                    }
                                } catch (e: HttpException) {
                                    err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                                } catch (e: Exception) {
                                    err = e.message
                                }
                            }
                        },
                        modifier = Modifier.fillMaxWidth().padding(top = 8.dp).semantics { contentDescription = UiStrings.Pix },
                    ) { Text(UiStrings.Pix) }
                }
            },
            confirmButton = {},
            dismissButton = {
                Button(onClick = { pick = null }) { Text(UiStrings.Voltar) }
            },
        )
    }

    if (showCreditConfirm && pick != null) {
        val pkg = pick!!
        AlertDialog(
            onDismissRequest = { showCreditConfirm = false },
            title = { Text(UiStrings.D2) },
            confirmButton = {
                Button(
                    onClick = {
                        scope.launch {
                            try {
                                api.clientBuy(
                                    UUID.randomUUID().toString(),
                                    ClientBuyBody(pkg.id, "CREDIT"),
                                )
                                Toast.makeText(ctx, UiStrings.T8, Toast.LENGTH_SHORT).show()
                                showCreditConfirm = false
                                pick = null
                                onCreditDone()
                            } catch (e: HttpException) {
                                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                            } catch (e: Exception) {
                                err = e.message
                            }
                        }
                    },
                ) { Text(UiStrings.Confirmar) }
            },
            dismissButton = {
                Button(onClick = { showCreditConfirm = false }) { Text(UiStrings.Voltar) }
            },
        )
    }
}
