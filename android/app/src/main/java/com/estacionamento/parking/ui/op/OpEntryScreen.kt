package com.estacionamento.parking.ui.op

import android.widget.Toast
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.CreateTicketBody
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.offline.OfflineQueueItem
import com.estacionamento.parking.offline.OfflineQueueStore
import com.estacionamento.parking.plate.PlateOutlinedTextField
import com.estacionamento.parking.plate.PlateValidator
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.launch
import org.json.JSONObject
import retrofit2.HttpException
import java.util.UUID

@Composable
fun OpEntryScreen(
    api: ParkingApi,
    offlineStore: OfflineQueueStore,
    isOnline: () -> Boolean,
    onDone: () -> Unit,
    onBack: () -> Unit,
    onQueued: () -> Unit,
) {
    val ctx = LocalContext.current.applicationContext
    val keyboard = LocalSoftwareKeyboardController.current
    var plate by remember { mutableStateOf("") }
    var err by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    fun trySubmit() {
        val n = PlateValidator.normalize(plate)
        if (!PlateValidator.isValidNormalized(n)) {
            err = UiStrings.E4
            return
        }
        err = null
        if (!isOnline()) {
            offlineStore.enqueue(
                OfflineQueueItem(
                    idLocal = UUID.randomUUID().toString(),
                    path = "tickets",
                    idempotencyKey = UUID.randomUUID().toString(),
                    bodyJson = JSONObject().put("plate", n).toString(),
                    createdAtEpoch = System.currentTimeMillis(),
                ),
            )
            Toast.makeText(ctx, UiStrings.TQueueSync, Toast.LENGTH_LONG).show()
            onQueued()
            return
        }
        scope.launch {
            try {
                api.createTicket(UUID.randomUUID().toString(), CreateTicketBody(n))
                Toast.makeText(ctx, UiStrings.T2, Toast.LENGTH_SHORT).show()
                onDone()
            } catch (e: HttpException) {
                err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
            } catch (e: Exception) {
                err = e.message
            }
        }
    }

    Column(Modifier.padding(16.dp)) {
        Text(
            "Nova entrada",
            style = MaterialTheme.typography.titleLarge,
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(bottom = 12.dp),
        )
        PlateOutlinedTextField(
            value = plate,
            onValueChange = { plate = it },
            label = { Text(UiStrings.Placa) },
            modifier = Modifier.fillMaxWidth(),
            keyboardOptions =
                KeyboardOptions(
                    capitalization = KeyboardCapitalization.Characters,
                    keyboardType = KeyboardType.Text,
                    imeAction = ImeAction.Done,
                ),
            keyboardActions =
                KeyboardActions(
                    onDone = {
                        keyboard?.hide()
                        trySubmit()
                    },
                ),
        )
        err?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        Button(
            onClick = { trySubmit() },
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = 8.dp)
                .semantics { contentDescription = UiStrings.Confirmar },
        ) {
            Text(UiStrings.Confirmar)
        }
        Button(onClick = onBack, modifier = Modifier.padding(top = 8.dp)) {
            Text(UiStrings.Voltar)
        }
    }
}
