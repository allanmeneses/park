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

import androidx.compose.runtime.mutableStateOf

import androidx.compose.runtime.remember

import androidx.compose.runtime.rememberCoroutineScope

import androidx.compose.runtime.setValue

import androidx.compose.ui.Modifier

import androidx.compose.ui.semantics.contentDescription

import androidx.compose.ui.semantics.semantics

import androidx.compose.ui.unit.dp

import com.estacionamento.parking.errors.ApiErrorMapper

import com.estacionamento.parking.network.CardPayBody

import com.estacionamento.parking.network.ParkingApi

import com.estacionamento.parking.ui.UiStrings

import kotlinx.coroutines.launch

import retrofit2.HttpException



@Composable

fun OpPayCardScreen(

    api: ParkingApi,

    paymentId: String,

    onSuccess: () -> Unit,

    onAmountMismatch: () -> Unit,

    onBack: () -> Unit,

) {

    var amount by remember { mutableStateOf<String?>(null) }

    var err by remember { mutableStateOf<String?>(null) }

    val scope = rememberCoroutineScope()



    LaunchedEffect(paymentId) {

        try {

            amount = api.getPayment(paymentId).amount

        } catch (e: HttpException) {

            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())

        } catch (e: Exception) {

            err = e.message

        }

    }



    Column(Modifier.padding(16.dp)) {

        Button(onClick = onBack, modifier = Modifier.padding(bottom = 8.dp)) {

            Text(UiStrings.Voltar)

        }

        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }

        amount?.let { Text("Valor: R\$ $it") }

        Button(

            onClick = {

                val a = amount?.toDoubleOrNull() ?: return@Button

                scope.launch {

                    try {

                        api.payCard(CardPayBody(paymentId, a))

                        onSuccess()

                    } catch (e: HttpException) {

                        val body = e.response()?.errorBody()?.string().orEmpty()

                        if (body.contains("AMOUNT_MISMATCH")) onAmountMismatch()

                        else err = ApiErrorMapper.resolve(body)

                    } catch (e: Exception) {

                        err = e.message

                    }

                }

            },

            modifier = Modifier

                .fillMaxWidth()

                .padding(top = 16.dp)

                .semantics { contentDescription = UiStrings.Confirmar },

            enabled = amount != null,

        ) {

            Text(UiStrings.Confirmar)

        }

    }

}


