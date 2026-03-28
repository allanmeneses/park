package com.estacionamento.parking.ui.common

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.ui.UiStrings

@Composable
fun PlaceholderScreen(title: String, onBack: () -> Unit) {
    Column(Modifier.padding(16.dp)) {
        Text(title)
        Button(
            onClick = onBack,
            modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.Voltar },
        ) {
            Text(UiStrings.Voltar)
        }
    }
}
