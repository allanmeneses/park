package com.estacionamento.parking.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

/** SPEC_FRONTEND §7 — tokens fixos (Material 3). */
private val Primary = Color(0xFF1565C0)
private val OnPrimary = Color(0xFFFFFFFF)
private val Error = Color(0xFFC62828)
private val Surface = Color(0xFFFFFFFF)
private val OnSurface = Color(0xFF212121)
private val OnSurfaceVariant = Color(0xFF757575)

private val ParkingLightColors = lightColorScheme(
    primary = Primary,
    onPrimary = OnPrimary,
    error = Error,
    surface = Surface,
    onSurface = OnSurface,
    onSurfaceVariant = OnSurfaceVariant,
)

private val ParkingTypography = Typography(
    titleLarge = TextStyle(
        fontWeight = FontWeight.SemiBold,
        fontSize = 20.sp,
        lineHeight = 24.sp,
    ),
    bodyLarge = TextStyle(
        fontWeight = FontWeight.Normal,
        fontSize = 16.sp,
        lineHeight = 22.sp,
    ),
)

@Composable
fun ParkingTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = ParkingLightColors,
        typography = ParkingTypography,
        content = content,
    )
}
