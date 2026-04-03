package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
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
import android.widget.Toast
import androidx.compose.ui.platform.LocalContext
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.RechargePackageDto
import com.estacionamento.parking.network.SettingsPostBody
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException

@Composable
fun MgrSettingsScreen(
    api: ParkingApi,
    role: String,
    onBack: () -> Unit,
) {
    val ctx = LocalContext.current
    val canLojInvites = role == "ADMIN" || role == "SUPER_ADMIN"
    var priceStr by remember { mutableStateOf("") }
    var capStr by remember { mutableStateOf("") }
    var clientPkgs by remember { mutableStateOf<List<RechargePackageDto>>(emptyList()) }
    var lojPkgs by remember { mutableStateOf<List<RechargePackageDto>>(emptyList()) }
    var err by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        try {
            val s = api.settings()
            priceStr = s.pricePerHour
            capStr = s.capacity.toString()
            clientPkgs = api.rechargePackages("CLIENT").items
            lojPkgs = api.rechargePackages("LOJISTA").items
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
        Text(UiStrings.B13, style = MaterialTheme.typography.titleMedium)
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        if (canLojInvites) {
            MgrLojistaInvitesSection(api = api)
        }
        OutlinedTextField(
            value = priceStr,
            onValueChange = { priceStr = it },
            label = { Text("Preço por hora (R\$)") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )
        OutlinedTextField(
            value = capStr,
            onValueChange = { capStr = it },
            label = { Text("Capacidade (vagas)") },
            modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
            singleLine = true,
        )
        Button(
            onClick = {
                val price = priceStr.replace(",", ".").toDoubleOrNull()
                val cap = capStr.toIntOrNull()
                if (price == null || price < 0.01) {
                    err = "Preço inválido."
                    return@Button
                }
                if (cap == null || cap < 1) {
                    err = "Capacidade inválida."
                    return@Button
                }
                scope.launch {
                    try {
                        api.settingsPost(SettingsPostBody(price, cap))
                        Toast.makeText(ctx, UiStrings.T7, Toast.LENGTH_SHORT).show()
                        err = null
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
                .semantics { contentDescription = UiStrings.Salvar },
        ) {
            Text(UiStrings.Salvar)
        }
        Text("Pacotes — CLIENTE", modifier = Modifier.padding(top = 24.dp), style = MaterialTheme.typography.titleSmall)
        if (clientPkgs.isEmpty()) {
            Text(UiStrings.S12)
        } else {
            clientPkgs.forEach { p ->
                Text("${p.hours} h — R\$ ${p.price}", modifier = Modifier.padding(vertical = 4.dp))
            }
        }
        Text("Pacotes — LOJISTA", modifier = Modifier.padding(top = 16.dp), style = MaterialTheme.typography.titleSmall)
        if (lojPkgs.isEmpty()) {
            Text(UiStrings.S12)
        } else {
            lojPkgs.forEach { p ->
                Text("${p.hours} h — R\$ ${p.price}", modifier = Modifier.padding(vertical = 4.dp))
            }
        }
    }
}
