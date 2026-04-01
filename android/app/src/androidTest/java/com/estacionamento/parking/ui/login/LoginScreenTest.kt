package com.estacionamento.parking.ui.login

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import com.estacionamento.parking.ComposeTestActivity
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.estacionamento.parking.auth.AuthPrefs
import com.estacionamento.parking.network.ParkingApiFactory
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class LoginScreenTest {
    @get:Rule
    val rule = createAndroidComposeRule<ComposeTestActivity>()

    @Test
    fun entrarButton_hasAccessibilityLabel() {
        val ctx = InstrumentationRegistry.getInstrumentation().targetContext
        val prefs = AuthPrefs(ctx.applicationContext)
        val api = ParkingApiFactory.create("http://127.0.0.1:9/api/v1", prefs)
        rule.setContent {
            MaterialTheme {
                LoginScreen(api = api, prefs = prefs, onLoggedIn = {})
            }
        }
        rule.onNodeWithContentDescription(com.estacionamento.parking.ui.UiStrings.B1).assertExists()
    }
}
