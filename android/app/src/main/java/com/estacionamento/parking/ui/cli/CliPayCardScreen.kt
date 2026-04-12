package com.estacionamento.parking.ui.cli

import androidx.compose.runtime.Composable
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.ui.loj.LojPayCardScreen

@Composable
fun CliPayCardScreen(
    api: ParkingApi,
    paymentId: String,
    apiRootUrl: String,
    accessToken: String,
    onPaid: () -> Unit,
    onBack: () -> Unit,
) {
    LojPayCardScreen(
        api = api,
        paymentId = paymentId,
        apiRootUrl = apiRootUrl,
        accessToken = accessToken,
        onPaid = onPaid,
        onBack = onBack,
    )
}
