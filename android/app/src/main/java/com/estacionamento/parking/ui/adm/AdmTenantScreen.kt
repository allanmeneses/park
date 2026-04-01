package com.estacionamento.parking.ui.adm

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
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
import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.AdminCreateTenantBody
import com.estacionamento.parking.network.AdminTenantListItem
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import retrofit2.HttpException
import java.util.UUID

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AdmTenantScreen(
    api: ParkingApi,
    prefs: AuthPrefs,
    onGestao: () -> Unit,
    onOperacao: () -> Unit,
    onLogout: () -> Unit,
) {
    var uuid by remember { mutableStateOf(prefs.activeParkingId.orEmpty()) }
    var tenants by remember { mutableStateOf<List<AdminTenantListItem>>(emptyList()) }
    var cAdmEmail by remember { mutableStateOf("") }
    var cAdmPass by remember { mutableStateOf("") }
    var cOpEmail by remember { mutableStateOf("") }
    var cOpPass by remember { mutableStateOf("") }
    var creating by remember { mutableStateOf(false) }
    val snack = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val scroll = rememberScrollState()

    LaunchedEffect(Unit) {
        try {
            tenants = api.adminTenants().items
        } catch (_: Exception) {
            /* lista opcional */
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snack) },
    ) { padding ->
        Column(
            Modifier
                .padding(padding)
                .padding(16.dp)
                .verticalScroll(scroll),
        ) {
            Text("Super administrador", style = MaterialTheme.typography.headlineSmall)
            Text(
                "Só o super administrador cria estacionamentos. O ADMIN do tenant acede só ao seu.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(bottom = 16.dp),
            )

            Text("Novo estacionamento", style = MaterialTheme.typography.titleMedium)
            OutlinedTextField(
                value = cAdmEmail,
                onValueChange = { cAdmEmail = it },
                label = { Text("E-mail administrador") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            OutlinedTextField(
                value = cAdmPass,
                onValueChange = { cAdmPass = it },
                label = { Text("Senha administrador") },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                singleLine = true,
            )
            OutlinedTextField(
                value = cOpEmail,
                onValueChange = { cOpEmail = it },
                label = { Text("E-mail operador") },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                singleLine = true,
            )
            OutlinedTextField(
                value = cOpPass,
                onValueChange = { cOpPass = it },
                label = { Text("Senha operador") },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
                singleLine = true,
            )
            Button(
                onClick = {
                    val ae = cAdmEmail.trim()
                    val oe = cOpEmail.trim()
                    if (ae.isEmpty() || cAdmPass.isEmpty() || oe.isEmpty() || cOpPass.isEmpty()) {
                        scope.launch { snack.showSnackbar(UiStrings.E3) }
                        return@Button
                    }
                    if (ae.equals(oe, ignoreCase = true)) {
                        scope.launch { snack.showSnackbar("Administrador e operador: e-mails diferentes.") }
                        return@Button
                    }
                    scope.launch {
                        creating = true
                        try {
                            api.adminCreateTenant(
                                AdminCreateTenantBody(
                                    adminEmail = ae,
                                    adminPassword = cAdmPass,
                                    operatorEmail = oe,
                                    operatorPassword = cOpPass,
                                ),
                            )
                            cAdmEmail = ""
                            cAdmPass = ""
                            cOpEmail = ""
                            cOpPass = ""
                            tenants = api.adminTenants().items
                            snack.showSnackbar("Estacionamento criado.")
                        } catch (e: HttpException) {
                            snack.showSnackbar(ApiErrorMapper.resolve(e.response()?.errorBody()?.string()))
                        } catch (_: Exception) {
                            snack.showSnackbar("Falha ao criar.")
                        } finally {
                            creating = false
                        }
                    }
                },
                enabled = !creating,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 12.dp),
            ) {
                Text(if (creating) "A criar…" else "Criar estacionamento")
            }

            Text("Lista", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 24.dp))
            tenants.forEach { t ->
                Button(
                    onClick = {
                        uuid = t.parkingId.lowercase()
                        prefs.activeParkingId = uuid
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 4.dp),
                ) {
                    Text(t.label.ifBlank { t.parkingId })
                }
            }

            Text("UUID manual", style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 16.dp))
            OutlinedTextField(
                value = uuid,
                onValueChange = { uuid = it },
                label = { Text("UUID do estacionamento") },
                modifier = Modifier
                    .fillMaxWidth()
                    .semantics { contentDescription = UiStrings.FieldParkingUuid },
                singleLine = true,
            )
            Button(
                onClick = {
                    val t = uuid.trim()
                    if (runCatching { UUID.fromString(t) }.isFailure) {
                        scope.launch { snack.showSnackbar(UiStrings.S15) }
                        return@Button
                    }
                    prefs.activeParkingId = t
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp)
                    .semantics { contentDescription = UiStrings.Continuar },
            ) {
                Text(UiStrings.Continuar)
            }
            Button(
                onClick = {
                    if (prefs.activeParkingId.isNullOrBlank()) {
                        scope.launch { snack.showSnackbar(UiStrings.S15) }
                    } else {
                        onGestao()
                    }
                },
                modifier = Modifier.padding(top = 8.dp).semantics { contentDescription = UiStrings.B20 },
            ) {
                Text(UiStrings.B20)
            }
            Button(
                onClick = {
                    if (prefs.activeParkingId.isNullOrBlank()) {
                        scope.launch { snack.showSnackbar(UiStrings.S15) }
                    } else {
                        onOperacao()
                    }
                },
                modifier = Modifier.padding(top = 4.dp).semantics { contentDescription = UiStrings.B21 },
            ) {
                Text(UiStrings.B21)
            }
            Button(
                onClick = onLogout,
                modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.Sair },
            ) {
                Text(UiStrings.Sair)
            }
        }
    }
}
