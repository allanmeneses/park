package com.estacionamento.parking.ui.theme

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Identidade visual premium e leve: petróleo + superfície quente (referência comum em apps de
 * mobilidade/estacionamento — contraste claro, sem o azul Material genérico).
 */
private val Petrol = Color(0xFF134252)
private val PetrolDark = Color(0xFF0C2F38)
private val OnPrimary = Color(0xFFFFFFFF)
private val WarmSurface = Color(0xFFF6F3EF)
private val SurfaceVariant = Color(0xFFE8E3DC)
private val OnSurface = Color(0xFF1C2529)
private val OnSurfaceVariant = Color(0xFF5C656B)
private val Error = Color(0xFFB3261E)
private val Outline = Color(0xFFADA59C)
private val Secondary = Color(0xFF3D5A66)
private val OnSecondary = Color(0xFFFFFFFF)
private val Tertiary = Color(0xFF7A5F3E)
private val OnTertiary = Color(0xFFFFFFFF)

private val ParkingLightColors =
    lightColorScheme(
        primary = Petrol,
        onPrimary = OnPrimary,
        primaryContainer = Color(0xFFB8D4DC),
        onPrimaryContainer = PetrolDark,
        secondary = Secondary,
        onSecondary = OnSecondary,
        secondaryContainer = Color(0xFFD2E4ED),
        onSecondaryContainer = Color(0xFF1A3138),
        tertiary = Tertiary,
        onTertiary = OnTertiary,
        tertiaryContainer = Color(0xFFE8D4BC),
        onTertiaryContainer = Color(0xFF2B1F0D),
        error = Error,
        onError = Color(0xFFFFFFFF),
        background = WarmSurface,
        onBackground = OnSurface,
        surface = WarmSurface,
        onSurface = OnSurface,
        surfaceVariant = SurfaceVariant,
        onSurfaceVariant = OnSurfaceVariant,
        outline = Outline,
    )

private val ParkingTypography =
    Typography(
        titleLarge =
            TextStyle(
                fontFamily = FontFamily.SansSerif,
                fontWeight = FontWeight.SemiBold,
                fontSize = 22.sp,
                lineHeight = 28.sp,
                letterSpacing = (-0.2).sp,
            ),
        titleMedium =
            TextStyle(
                fontFamily = FontFamily.SansSerif,
                fontWeight = FontWeight.Medium,
                fontSize = 18.sp,
                lineHeight = 24.sp,
                letterSpacing = (-0.1).sp,
            ),
        bodyLarge =
            TextStyle(
                fontFamily = FontFamily.SansSerif,
                fontWeight = FontWeight.Normal,
                fontSize = 16.sp,
                lineHeight = 22.sp,
            ),
        labelLarge =
            TextStyle(
                fontFamily = FontFamily.SansSerif,
                fontWeight = FontWeight.Medium,
                fontSize = 14.sp,
                lineHeight = 20.sp,
                letterSpacing = 0.1.sp,
            ),
    )

private val ParkingShapes =
    Shapes(
        extraSmall = RoundedCornerShape(10.dp),
        small = RoundedCornerShape(12.dp),
        medium = RoundedCornerShape(16.dp),
        large = RoundedCornerShape(20.dp),
        extraLarge = RoundedCornerShape(24.dp),
    )

@Composable
fun ParkingTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = ParkingLightColors,
        typography = ParkingTypography,
        shapes = ParkingShapes,
        content = content,
    )
}
