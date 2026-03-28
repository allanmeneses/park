package com.estacionamento.parking.ui.adm

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
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
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import java.util.UUID

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AdmTenantScreen(
    prefs: AuthPrefs,
    onGestao: () -> Unit,
    onOperacao: () -> Unit,
    onLogout: () -> Unit,
) {
    var uuid by remember { mutableStateOf(prefs.activeParkingId.orEmpty()) }
    val snack = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    Scaffold(
        snackbarHost = { SnackbarHost(snack) },
    ) { padding ->
        Column(Modifier.padding(padding).padding(16.dp)) {
            Text("Tenant (SUPER_ADMIN)")
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
