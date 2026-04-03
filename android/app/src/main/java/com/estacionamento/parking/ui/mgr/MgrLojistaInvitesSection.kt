package com.estacionamento.parking.ui.mgr

import android.widget.Toast
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.LojistaInviteCreateBody
import com.estacionamento.parking.network.LojistaInviteListItemDto
import com.estacionamento.parking.network.ParkingApi
import kotlinx.coroutines.launch
import retrofit2.HttpException

/** Bloco de convites para cadastro de lojista (ADMIN / SUPER_ADMIN). */
@Composable
fun MgrLojistaInvitesSection(api: ParkingApi) {
    val ctx = LocalContext.current
    var lojInvites by remember { mutableStateOf<List<LojistaInviteListItemDto>>(emptyList()) }
    var inviteDisplay by remember { mutableStateOf("") }
    var lastMerchant by remember { mutableStateOf("") }
    var lastActivation by remember { mutableStateOf("") }
    var inviteErr by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        try {
            lojInvites = api.lojistaInvites().items
        } catch (_: Exception) {
            /* ignore */
        }
    }

    Text(
        "Convites — Lojista",
        modifier = Modifier.padding(top = 16.dp),
        style = MaterialTheme.typography.titleSmall,
    )
    Text(
        "Gere códigos para o lojista se cadastrar. O código de ativação só aparece uma vez.",
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    OutlinedTextField(
        value = inviteDisplay,
        onValueChange = { inviteDisplay = it },
        label = { Text("Nome exibido (opcional)") },
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp),
        singleLine = true,
    )
    Button(
        onClick = {
            inviteErr = null
            lastMerchant = ""
            lastActivation = ""
            scope.launch {
                try {
                    val r = api.lojistaInvitesCreate(
                        LojistaInviteCreateBody(
                            displayName = inviteDisplay.trim().takeIf { it.isNotBlank() },
                        ),
                    )
                    lastMerchant = r.merchantCode
                    lastActivation = r.activationCode
                    lojInvites = api.lojistaInvites().items
                    Toast.makeText(ctx, "Convite gerado.", Toast.LENGTH_SHORT).show()
                } catch (e: HttpException) {
                    inviteErr = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                } catch (e: Exception) {
                    inviteErr = e.message
                }
            }
        },
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 8.dp)
            .semantics { contentDescription = "Gerar convite de lojista" },
    ) {
        Text("Gerar convite")
    }
    inviteErr?.let { Text(it, color = MaterialTheme.colorScheme.error) }
    if (lastMerchant.isNotEmpty() && lastActivation.isNotEmpty()) {
        Text(
            "Código do lojista: $lastMerchant\nCódigo de ativação: $lastActivation\n(copie agora)",
            modifier = Modifier.padding(top = 8.dp),
            style = MaterialTheme.typography.bodyMedium,
        )
    }
    Text("Lojistas do estacionamento", modifier = Modifier.padding(top = 12.dp), style = MaterialTheme.typography.titleSmall)
    Text(
        "Todos os lojistas (pendentes e ativos). Horas e saldo só após ativação.",
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
    if (lojInvites.isEmpty()) {
        Text("Nenhum lojista ainda.", color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 4.dp))
    } else {
        lojInvites.forEach { inv ->
            val st = if (inv.activated) "Ativado" else "Pendente"
            val codeLine = inv.merchantCode?.let { "Código: $it" } ?: "Código público: —"
            Column(Modifier.padding(vertical = 8.dp)) {
                Text(
                    "${inv.shopName ?: "—"} · $st",
                    style = MaterialTheme.typography.titleSmall,
                )
                Text(codeLine, style = MaterialTheme.typography.bodyMedium)
                if (inv.activated) {
                    inv.email?.let { em ->
                        Text("E-mail: $em", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    val bought = inv.totalPurchasedHours ?: 0
                    val bal = inv.balanceHours ?: 0
                    Text(
                        "Horas compradas: $bought · Saldo: $bal h",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            }
        }
    }
}
