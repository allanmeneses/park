package com.estacionamento.parking.ui.cli

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
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
import com.estacionamento.parking.network.ClientBuyBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.RechargePackageDto
import com.estacionamento.parking.network.RechargePackages
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.UUID

@Composable
fun CliBuyScreen(
    api: ParkingApi,
    onBack: () -> Unit,
    onPayPix: (paymentId: String) -> Unit,
    onPayCard: (paymentId: String) -> Unit,
) {
    var items by remember { mutableStateOf<List<RechargePackageDto>>(emptyList()) }
    var err by remember { mutableStateOf<String?>(null) }
    var selectedPkg by remember { mutableStateOf<RechargePackageDto?>(null) }
    val scope = rememberCoroutineScope()
    val minMercadoPagoCardAmount = 1.0

    LaunchedEffect(Unit) {
        try {
            items = api.rechargePackages("CLIENT").items.sortedWith(RechargePackages::compare)
        } catch (e: HttpException) {
            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
        } catch (e: Exception) {
            err = e.message
        }
    }

    Column(Modifier.padding(16.dp)) {
        Text("Comprar horas", style = MaterialTheme.typography.titleLarge)
        err?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp)) }
        if (items.isEmpty() && err == null) {
            Text("Nenhum pacote disponível no momento.", modifier = Modifier.padding(top = 12.dp))
        } else {
            LazyColumn(Modifier.padding(top = 12.dp)) {
                items(items, key = { it.id }) { pkg ->
                    val selected = selectedPkg?.id == pkg.id
                    Button(
                        onClick = { selectedPkg = pkg },
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 4.dp)
                            .semantics {
                                contentDescription = if (selected) "Pacote selecionado" else UiStrings.B18
                            },
                    ) {
                        Column(Modifier.fillMaxWidth()) {
                            Text(
                                buildString {
                                    append(RechargePackages.title(pkg))
                                    if (pkg.isPromo) append(" • Promocional")
                                    if (selected) append(" • Selecionado")
                                },
                            )
                            Text("${pkg.hours} h — R$ ${pkg.price}")
                        }
                    }
                }
            }
        }
        selectedPkg?.let { pkg ->
            val cardBelowMinimum = RechargePackages.priceNumber(pkg.price) < minMercadoPagoCardAmount
            Column(Modifier.padding(top = 16.dp)) {
                Text(UiStrings.S30, style = MaterialTheme.typography.titleMedium)
                Text(
                    "${RechargePackages.title(pkg)} — ${pkg.hours} h — R$ ${pkg.price}",
                    modifier = Modifier.padding(top = 4.dp),
                )
                Button(
                    onClick = {
                        scope.launch {
                            try {
                                val response = api.clientBuy(
                                    UUID.randomUUID().toString(),
                                    ClientBuyBody(pkg.id, "PIX"),
                                )
                                response.paymentId?.let(onPayPix)
                            } catch (e: HttpException) {
                                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                            } catch (e: Exception) {
                                err = e.message
                            }
                        }
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 12.dp)
                        .semantics { contentDescription = UiStrings.B35 },
                ) {
                    Text(UiStrings.B35)
                }
                OutlinedButton(
                    onClick = {
                        scope.launch {
                            try {
                                val response = api.clientBuy(
                                    UUID.randomUUID().toString(),
                                    ClientBuyBody(pkg.id, "CARD"),
                                )
                                response.paymentId?.let(onPayCard)
                            } catch (e: HttpException) {
                                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                            } catch (e: Exception) {
                                err = e.message
                            }
                        }
                    },
                    enabled = !cardBelowMinimum,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 8.dp)
                        .semantics { contentDescription = UiStrings.B36 },
                ) {
                    Text(UiStrings.B36)
                }
                Text(
                    if (cardBelowMinimum) UiStrings.S37 else UiStrings.S29,
                    modifier = Modifier.padding(top = 8.dp),
                )
            }
        }
        Button(
            onClick = onBack,
            modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.Voltar },
        ) {
            Text(UiStrings.Voltar)
        }
    }
}
