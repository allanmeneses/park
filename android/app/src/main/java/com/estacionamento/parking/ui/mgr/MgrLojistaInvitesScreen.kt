package com.estacionamento.parking.ui.mgr

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.UiStrings

@Composable
fun MgrLojistaInvitesScreen(
    api: ParkingApi,
    onBack: () -> Unit,
) {
    Column(Modifier.padding(16.dp)) {
        Button(
            onClick = onBack,
            modifier = Modifier
                .padding(bottom = 8.dp)
                .semantics { contentDescription = UiStrings.Voltar },
        ) {
            Text(UiStrings.Voltar)
        }
        Text(
            "Cadastro de lojistas",
            style = MaterialTheme.typography.titleLarge,
        )
        Text(
            "Gere aqui os códigos para novos lojistas se registarem na app.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.padding(top = 8.dp, bottom = 8.dp),
        )
        MgrLojistaInvitesSection(api = api)
    }
}
