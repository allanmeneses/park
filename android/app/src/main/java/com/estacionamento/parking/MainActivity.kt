package com.estacionamento.parking

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import com.estacionamento.parking.ui.ParkingApp
import com.estacionamento.parking.ui.theme.ParkingTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            ParkingTheme {
                ParkingApp()
            }
        }
    }
}
