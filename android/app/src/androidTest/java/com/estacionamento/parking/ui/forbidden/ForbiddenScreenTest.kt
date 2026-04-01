package com.estacionamento.parking.ui.forbidden

import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import com.estacionamento.parking.MainActivity
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.estacionamento.parking.ui.UiStrings
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ForbiddenScreenTest {
    @get:Rule
    val rule = createAndroidComposeRule<MainActivity>()

    @Test
    fun voltarInicioButton_hasAccessibilityLabel() {
        rule.setContent {
            MaterialTheme {
                ForbiddenScreen(onGoHome = {})
            }
        }
        rule.onNodeWithContentDescription(UiStrings.B11).assertExists()
    }
}
