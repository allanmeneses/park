package com.estacionamento.parking.ui.op



import androidx.compose.foundation.layout.Column

import androidx.compose.foundation.layout.fillMaxWidth

import androidx.compose.foundation.layout.padding

import androidx.compose.material3.Button

import androidx.compose.material3.MaterialTheme

import androidx.compose.material3.Text

import androidx.compose.runtime.Composable

import androidx.compose.runtime.LaunchedEffect

import androidx.compose.runtime.getValue

import androidx.compose.runtime.mutableIntStateOf

import androidx.compose.runtime.mutableStateOf

import androidx.compose.runtime.remember

import androidx.compose.runtime.rememberCoroutineScope

import androidx.compose.runtime.setValue

import androidx.compose.ui.Modifier

import androidx.compose.ui.semantics.contentDescription

import androidx.compose.ui.semantics.semantics

import androidx.compose.ui.unit.dp

import com.estacionamento.parking.errors.ApiErrorMapper

import com.estacionamento.parking.network.GetTicketResponse

import com.estacionamento.parking.network.ParkingApi

import com.estacionamento.parking.ui.UiStrings

import com.estacionamento.parking.util.formatApiInstantForDeviceLocal

import com.estacionamento.parking.util.parseApiInstant

import com.estacionamento.parking.util.elapsedWholeSeconds

import com.estacionamento.parking.util.formatElapsedPtBr

import kotlinx.coroutines.delay

import kotlinx.coroutines.launch

import java.time.Instant

import retrofit2.HttpException



@Composable

fun OpTicketDetailScreen(

    api: ParkingApi,

    ticketId: String,

    onBack: () -> Unit,

    onCheckout: (String) -> Unit,

    onPay: (paymentId: String, ticketId: String) -> Unit,

) {

    var data by remember { mutableStateOf<GetTicketResponse?>(null) }

    var err by remember { mutableStateOf<String?>(null) }

    val scope = rememberCoroutineScope()



    fun load() {

        scope.launch {

            err = null

            try {

                data = api.getTicket(ticketId)

            } catch (e: HttpException) {

                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())

            } catch (e: Exception) {

                err = e.message

            }

        }

    }



    LaunchedEffect(ticketId) { load() }



    Column(Modifier.padding(16.dp)) {

        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {

            Text(UiStrings.Voltar)

        }

        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }

        data?.let { d ->

            val t = d.ticket

            var tick by remember(ticketId) { mutableIntStateOf(0) }

            LaunchedEffect(ticketId, t.status) {

                if (t.status != "OPEN") return@LaunchedEffect

                while (true) {

                    delay(1_000)

                    tick++

                }

            }

            val elapsedLabel = remember(t.status, t.entryTime, t.exitTime, tick) {

                val entryInst = parseApiInstant(t.entryTime) ?: return@remember null

                val endInst =

                    when (t.status) {

                        "OPEN" -> Instant.now()

                        else -> {

                            val ex = t.exitTime ?: return@remember null

                            parseApiInstant(ex) ?: return@remember null

                        }

                    }

                formatElapsedPtBr(elapsedWholeSeconds(entryInst, endInst))

            }

            Text("Placa: ${t.plate}", style = MaterialTheme.typography.titleMedium)

            Text("Entrada: ${formatApiInstantForDeviceLocal(t.entryTime)}")

            t.exitTime?.let { ex -> Text("Saída: ${formatApiInstantForDeviceLocal(ex)}") }

            Text("Status: ${t.status}")

            elapsedLabel?.let { label ->

                Text(
                    "Tempo decorrido: $label" + if (t.status == "OPEN") " (ao vivo)" else "",
                    modifier = Modifier.padding(top = 4.dp),
                )

            }

            when (t.status) {

                "OPEN" -> {

                    Button(

                        onClick = { onCheckout(ticketId) },

                        modifier = Modifier

                            .fillMaxWidth()

                            .padding(top = 16.dp)

                            .semantics { contentDescription = UiStrings.B4 },

                    ) {

                        Text(UiStrings.B4)

                    }

                }

                "AWAITING_PAYMENT" -> {

                    val pid = d.payment?.id

                    if (pid != null) {

                        Button(

                            onClick = { onPay(pid, ticketId) },

                            modifier = Modifier

                                .fillMaxWidth()

                                .padding(top = 16.dp)

                                .semantics { contentDescription = UiStrings.B5 },

                        ) {

                            Text(UiStrings.B5)

                        }

                    }

                }

                "CLOSED" -> Text(UiStrings.S4, modifier = Modifier.padding(top = 16.dp))

            }

        }

    }

}


