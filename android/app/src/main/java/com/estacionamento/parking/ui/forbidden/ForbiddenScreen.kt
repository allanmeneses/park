package com.estacionamento.parking.ui.forbidden

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
fun ForbiddenScreen(onGoHome: () -> Unit) {
    Column(Modifier.padding(16.dp)) {
        Text(UiStrings.S9)
        Text(UiStrings.S10, modifier = Modifier.padding(top = 8.dp))
        Button(
            onClick = onGoHome,
            modifier = Modifier.padding(top = 16.dp).semantics { contentDescription = UiStrings.B11 },
        ) {
            Text(UiStrings.B11)
        }
    }
}
