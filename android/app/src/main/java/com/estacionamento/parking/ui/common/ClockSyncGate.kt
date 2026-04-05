package com.estacionamento.parking.ui.common

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.util.isNetworkConnected
import com.estacionamento.parking.util.shouldBlockAppForClock
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import okhttp3.OkHttpClient

/**
 * Com rede: consulta GET /health e bloqueia a app se data/hora do dispositivo divergirem do servidor
 * (data civil em Brasília + margem de 5 min). Offline: não bloqueia.
 */
@Composable
fun ClockSyncGate(
    http: OkHttpClient,
    apiBase: String,
    content: @Composable () -> Unit,
) {
    val ctx = LocalContext.current
    val scope = rememberCoroutineScope()
    val lifecycleOwner = LocalLifecycleOwner.current
    var blocked by remember { mutableStateOf(false) }

    fun online(): Boolean = ctx.isNetworkConnected()

    suspend fun recompute() {
        blocked = when {
            !online() -> false
            else -> shouldBlockAppForClock(http, apiBase)
        }
    }

    LaunchedEffect(Unit) {
        while (isActive) {
            recompute()
            delay(30_000)
        }
    }

    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                scope.launch { recompute() }
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    if (blocked && online()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(24.dp),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(
                UiStrings.S25,
                color = Color(0xFFC62828),
                fontSize = 20.sp,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center,
                lineHeight = 26.sp,
            )
        }
    } else {
        content()
    }
}
