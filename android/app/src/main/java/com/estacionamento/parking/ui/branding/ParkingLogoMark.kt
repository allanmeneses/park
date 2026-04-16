package com.estacionamento.parking.ui.branding

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/**
 * Marca minimalista: silhueta de veículo + faixa (estrada) — leve, vetorial, sem assets externos.
 */
@Composable
fun ParkingLogoMark(
    modifier: Modifier = Modifier,
    size: Dp = 48.dp,
    contentColor: Color = MaterialTheme.colorScheme.primary,
) {
    Canvas(modifier = modifier.size(size)) {
        val w = this.size.width
        val h = this.size.height
        val bodyW = w * 0.72f
        val bodyH = h * 0.42f
        val left = (w - bodyW) / 2f
        val top = h * 0.14f
        drawRoundRect(
            color = contentColor,
            topLeft = Offset(left, top),
            size = Size(bodyW, bodyH),
            cornerRadius = CornerRadius(w * 0.09f, w * 0.09f),
        )
        drawRoundRect(
            color = contentColor,
            topLeft = Offset(w * 0.1f, h * 0.72f),
            size = Size(w * 0.8f, h * 0.12f),
            cornerRadius = CornerRadius(h * 0.04f, h * 0.04f),
        )
    }
}
