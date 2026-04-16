package com.estacionamento.parking.ui.op

import android.widget.Toast
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.ExperimentalMaterialApi
import androidx.compose.material.pullrefresh.PullRefreshIndicator
import androidx.compose.material.pullrefresh.pullRefresh
import androidx.compose.material.pullrefresh.rememberPullRefreshState
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.key
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.ParkingApiFactory
import com.estacionamento.parking.network.TicketOpenItem
import com.estacionamento.parking.ui.UiStrings
import com.estacionamento.parking.util.formatApiInstantForDeviceLocal
import com.estacionamento.parking.util.elapsedWholeSeconds
import com.estacionamento.parking.util.formatElapsedPtBr
import com.estacionamento.parking.util.isNetworkConnected
import com.estacionamento.parking.util.parseApiInstant
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.time.Instant
import retrofit2.HttpException

private enum class OpHomeListPhase {
    Loading,
    Error,
    Empty,
    HasTickets,
}

@OptIn(ExperimentalMaterialApi::class)
@Composable
fun OpHomeScreen(
    api: ParkingApi,
    onNewEntry: () -> Unit,
    onTicket: (String) -> Unit,
    onLogout: () -> Unit,
) {
    val ctx = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    var items by remember { mutableStateOf<List<TicketOpenItem>>(emptyList()) }
    var loading by remember { mutableStateOf(true) }
    var isRefreshing by remember { mutableStateOf(false) }
    var err by remember { mutableStateOf<String?>(null) }
    var online by remember { mutableStateOf(ctx.isNetworkConnected()) }
    var homeBillableTick by remember { mutableIntStateOf(0) }
    val scope = rememberCoroutineScope()

    LaunchedEffect(items.any { it.status == "OPEN" }) {
        if (!items.any { it.status == "OPEN" }) return@LaunchedEffect
        while (true) {
            delay(1_000)
            homeBillableTick++
        }
    }

    fun runLoad(fromPull: Boolean) {
        scope.launch {
            when {
                fromPull -> isRefreshing = true
                items.isEmpty() -> loading = true
            }
            err = null
            try {
                items = api.openTickets().items
            } catch (e: HttpException) {
                if (items.isEmpty()) {
                    err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
                }
            } catch (e: Exception) {
                if (items.isEmpty()) err = e.message
            } finally {
                loading = false
                isRefreshing = false
            }
        }
    }

    LaunchedEffect(Unit) {
        online = ctx.isNetworkConnected()
        runLoad(fromPull = false)
    }

    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME) {
                online = ctx.isNetworkConnected()
                runLoad(fromPull = false)
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    val pullState = rememberPullRefreshState(isRefreshing, onRefresh = { runLoad(fromPull = true) })

    Box(
        Modifier
            .fillMaxSize()
            .padding(16.dp)
            .pullRefresh(pullState),
    ) {
        Column(Modifier.fillMaxSize()) {
            if (!online) {
                Text(UiStrings.S2, color = MaterialTheme.colorScheme.error)
            }
            Row(
                Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    "Operador",
                    style = MaterialTheme.typography.titleLarge,
                    color = MaterialTheme.colorScheme.primary,
                )
                IconButton(
                    onClick = {
                        scope.launch {
                            try {
                                api.operatorProblem(ParkingApiFactory.emptyJsonBody)
                                Toast.makeText(ctx, UiStrings.T1, Toast.LENGTH_SHORT).show()
                            } catch (e: HttpException) {
                                Toast.makeText(
                                    ctx,
                                    ApiErrorMapper.resolve(e.response()?.errorBody()?.string()),
                                    Toast.LENGTH_SHORT,
                                ).show()
                            } catch (_: Exception) { }
                        }
                    },
                    modifier = Modifier.semantics { contentDescription = UiStrings.B3 },
                ) {
                    Text("⋮", fontSize = 22.sp)
                }
            }
            Button(
                onClick = onNewEntry,
                enabled = online,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 4.dp, bottom = 8.dp)
                    .semantics { contentDescription = UiStrings.B2 },
            ) {
                Text(UiStrings.B2)
            }
            val listPhase =
                when {
                    loading -> OpHomeListPhase.Loading
                    err != null && items.isEmpty() -> OpHomeListPhase.Error
                    items.isEmpty() -> OpHomeListPhase.Empty
                    else -> OpHomeListPhase.HasTickets
                }
            AnimatedContent(
                targetState = listPhase,
                modifier =
                    Modifier
                        .fillMaxWidth()
                        .weight(1f),
                transitionSpec = {
                    fadeIn(animationSpec = tween(220)) togetherWith fadeOut(animationSpec = tween(180))
                },
                label = "opHomeTickets",
            ) { phase ->
                when (phase) {
                    OpHomeListPhase.Loading ->
                        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            CircularProgressIndicator()
                        }
                    OpHomeListPhase.Error ->
                        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            Text(
                                err.orEmpty(),
                                color = MaterialTheme.colorScheme.error,
                                style = MaterialTheme.typography.bodyLarge,
                            )
                        }
                    OpHomeListPhase.Empty ->
                        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            Text(
                                UiStrings.S1,
                                style = MaterialTheme.typography.bodyLarge,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    OpHomeListPhase.HasTickets ->
                        Column(Modifier.fillMaxSize()) {
                            if (!online) {
                                Text(
                                    UiStrings.S3,
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.tertiary,
                                    modifier = Modifier.padding(bottom = 8.dp),
                                )
                            }
                            key(homeBillableTick) {
                                LazyColumn(Modifier.weight(1f)) {
                                    items(items, key = { it.id }) { t ->
                                        val whenStr = formatApiInstantForDeviceLocal(t.entryTime)
                                        val elapsedSuffix =
                                            if (t.status == "OPEN") {
                                                val entryInst = parseApiInstant(t.entryTime)
                                                if (entryInst != null) {
                                                    val sec = elapsedWholeSeconds(entryInst, Instant.now())
                                                    " — decorrido: ${formatElapsedPtBr(sec)}"
                                                } else {
                                                    ""
                                                }
                                            } else {
                                                ""
                                            }
                                        Text(
                                            "${t.plate} — ${t.status} — $whenStr$elapsedSuffix",
                                            modifier = Modifier
                                                .fillMaxWidth()
                                                .clickable { onTicket(t.id) }
                                                .padding(vertical = 8.dp),
                                        )
                                    }
                                }
                            }
                        }
                }
            }
            Button(onClick = onLogout, modifier = Modifier.padding(top = 16.dp)) {
                Text(UiStrings.Sair)
            }
        }
        PullRefreshIndicator(
            refreshing = isRefreshing,
            state = pullState,
            modifier = Modifier.align(Alignment.TopCenter),
        )
    }
}
