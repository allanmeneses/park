package com.estacionamento.parking.ui.loj

import android.annotation.SuppressLint
import android.webkit.JavascriptInterface
import android.webkit.WebChromeClient
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.unit.dp
import com.estacionamento.parking.errors.ApiErrorMapper
import com.estacionamento.parking.network.CardPayBody
import com.estacionamento.parking.network.CardPayOutcome
import com.estacionamento.parking.network.ParkingApi
import com.estacionamento.parking.network.toOutcome
import com.estacionamento.parking.ui.UiStrings
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import org.json.JSONObject
import retrofit2.HttpException

private data class EmbeddedCardWebConfig(
    val baseUrl: String,
    val accessToken: String,
    val paymentId: String,
    val amount: Double,
    val publicKey: String,
)

private class EmbeddedCardBridge(
    private val onStatus: (String, String?) -> Unit,
    private val onErrorCallback: (String) -> Unit,
) {
    @JavascriptInterface
    fun onPaymentStatus(status: String, message: String?) {
        onStatus(status, message?.takeIf { it.isNotBlank() })
    }

    @JavascriptInterface
    fun onError(message: String?) {
        onErrorCallback(message?.takeIf { it.isNotBlank() } ?: "Não foi possível carregar o pagamento com cartão.")
    }
}

@Composable
fun LojPayCardScreen(
    api: ParkingApi,
    paymentId: String,
    apiRootUrl: String,
    accessToken: String,
    onPaid: () -> Unit,
    onBack: () -> Unit,
) {
    var amount by remember { mutableStateOf<String?>(null) }
    var err by remember { mutableStateOf<String?>(null) }
    var webConfig by remember { mutableStateOf<EmbeddedCardWebConfig?>(null) }
    var pollGen by remember { mutableIntStateOf(0) }

    LaunchedEffect(paymentId) {
        try {
            val payment = api.getPayment(paymentId)
            amount = payment.amount
            if (payment.status.equals("PAID", ignoreCase = true)) {
                onPaid()
                return@LaunchedEffect
            }

            val dec = payment.amount.replace(",", ".").toDoubleOrNull()
            if (dec == null) {
                err = "Valor do pagamento inválido."
                return@LaunchedEffect
            }

            when (val outcome = api.payCard(CardPayBody(paymentId = paymentId, amount = dec, flow = "EMBEDDED")).toOutcome(false)) {
                is CardPayOutcome.EmbeddedBricks -> {
                    if (!outcome.provider.equals("mercadopago", ignoreCase = true) || outcome.publicKey.isNullOrBlank()) {
                        err = "Cartão embutido requer Mercado Pago configurado no servidor."
                    } else {
                        webConfig = EmbeddedCardWebConfig(
                            baseUrl = apiRootUrl,
                            accessToken = accessToken,
                            paymentId = paymentId,
                            amount = dec,
                            publicKey = outcome.publicKey,
                        )
                    }
                }
                is CardPayOutcome.SyncPaid -> onPaid()
                is CardPayOutcome.Pending -> pollGen++
                is CardPayOutcome.Failed -> err = outcome.message
                else -> err = "Resposta do servidor não reconhecida para cartão."
            }
        } catch (e: HttpException) {
            err = ApiErrorMapper.resolve(e.response()?.errorBody()?.string())
        } catch (e: Exception) {
            err = e.message
        }
    }

    LaunchedEffect(pollGen) {
        if (pollGen == 0) return@LaunchedEffect
        val deadline = System.currentTimeMillis() + 900_000L
        while (isActive && System.currentTimeMillis() < deadline) {
            delay(1_000)
            try {
                when (api.getPayment(paymentId).status.uppercase()) {
                    "PAID" -> {
                        onPaid()
                        return@LaunchedEffect
                    }
                    "FAILED" -> {
                        err = "Pagamento recusado ou falhou. Tente outro cartão ou use PIX."
                        return@LaunchedEffect
                    }
                    "EXPIRED" -> {
                        err = "Pagamento expirado. Gere uma nova tentativa."
                        return@LaunchedEffect
                    }
                }
            } catch (_: Exception) {
            }
        }
        err = UiStrings.S28
    }

    Column(Modifier.padding(16.dp)) {
        Text("Cartão", style = MaterialTheme.typography.titleLarge)
        amount?.let { Text("Valor: R\$ $it", modifier = Modifier.padding(top = 8.dp)) }
        err?.let { Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp)) }
        webConfig?.let { cfg ->
            EmbeddedCardWebView(
                config = cfg,
                onStatus = { status, message ->
                    when (status.uppercase()) {
                        "PAID" -> onPaid()
                        "PENDING" -> pollGen++
                        "FAILED", "EXPIRED" -> err = message ?: "Pagamento recusado."
                        else -> err = message ?: "Falha ao processar pagamento."
                    }
                },
                onError = { message -> err = message },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(640.dp)
                    .padding(top = 12.dp),
            )
        }
        Button(onClick = onBack, modifier = Modifier.padding(top = 16.dp)) {
            Text(UiStrings.Voltar)
        }
    }
}

@SuppressLint("SetJavaScriptEnabled")
@Composable
private fun EmbeddedCardWebView(
    config: EmbeddedCardWebConfig,
    onStatus: (String, String?) -> Unit,
    onError: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    val html = remember(config) { buildEmbeddedCardHtml(config) }
    val bridge = remember(onStatus, onError) { EmbeddedCardBridge(onStatus, onError) }
    var webViewRef by remember { mutableStateOf<WebView?>(null) }

    DisposableEffect(Unit) {
        onDispose {
            webViewRef?.removeJavascriptInterface("AndroidBridge")
            webViewRef?.destroy()
            webViewRef = null
        }
    }

    AndroidView(
        factory = { ctx ->
            WebView(ctx).apply {
                settings.javaScriptEnabled = true
                settings.domStorageEnabled = true
                settings.javaScriptCanOpenWindowsAutomatically = false
                webChromeClient = WebChromeClient()
                webViewClient = object : WebViewClient() {
                    override fun onReceivedError(view: WebView?, request: WebResourceRequest?, error: WebResourceError?) {
                        if (request?.isForMainFrame == true) {
                            onError(error?.description?.toString() ?: "Falha ao carregar o formulário de cartão.")
                        }
                    }
                }
                addJavascriptInterface(bridge, "AndroidBridge")
                tag = html
                loadDataWithBaseURL(config.baseUrl, html, "text/html", "utf-8", null)
                webViewRef = this
            }
        },
        update = { view ->
            if (view.tag != html) {
                view.tag = html
                view.loadDataWithBaseURL(config.baseUrl, html, "text/html", "utf-8", null)
            }
        },
        modifier = modifier,
    )
}

private fun jsQuoted(value: String): String = JSONObject.quote(value)

private fun buildEmbeddedCardHtml(config: EmbeddedCardWebConfig): String {
    val baseUrl = jsQuoted(config.baseUrl.trimEnd('/'))
    val authHeader = jsQuoted("Bearer ${config.accessToken}")
    val paymentId = jsQuoted(config.paymentId)
    val publicKey = jsQuoted(config.publicKey)
    return """
        <!doctype html>
        <html lang="pt-BR">
          <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <script src="https://sdk.mercadopago.com/js/v2"></script>
            <style>
              body { font-family: sans-serif; margin: 0; padding: 0; background: #fff; }
              #cardPaymentBrick_container { min-height: 520px; }
              .hint { color: #555; font-size: 14px; margin: 0 0 12px 0; }
            </style>
          </head>
          <body>
            <p class="hint">Pagamento processado pelo Mercado Pago em formulário seguro embutido.</p>
            <div id="cardPaymentBrick_container"></div>
            <script>
              const apiBase = $baseUrl;
              const authHeader = $authHeader;
              const paymentId = $paymentId;
              const amount = ${config.amount};
              const publicKey = $publicKey;

              function notifyStatus(status, message) {
                window.AndroidBridge.onPaymentStatus(String(status || ''), message == null ? '' : String(message));
              }

              function notifyError(message) {
                window.AndroidBridge.onError(message == null ? '' : String(message));
              }

              async function submitPayment(formData) {
                const response = await fetch(apiBase + '/api/v1/payments/card', {
                  method: 'POST',
                  headers: {
                    'Content-Type': 'application/json',
                    'Authorization': authHeader,
                  },
                  body: JSON.stringify({
                    paymentId,
                    amount,
                    flow: 'EMBEDDED',
                    token: formData.token || '',
                    installments: Number(formData.installments || 1),
                    paymentMethodId: formData.payment_method_id || '',
                    issuerId: formData.issuer_id == null ? null : String(formData.issuer_id),
                    payerEmail: formData.payer && formData.payer.email ? formData.payer.email : '',
                    identificationType: formData.payer && formData.payer.identification ? formData.payer.identification.type : null,
                    identificationNumber: formData.payer && formData.payer.identification ? formData.payer.identification.number : null,
                  }),
                });

                let payload = {};
                try { payload = await response.json(); } catch (_) {}

                if (!response.ok) {
                  const message = payload.message || payload.detail || 'Não foi possível processar o cartão.';
                  notifyError(message);
                  throw new Error(message);
                }

                const localStatus = String(payload.status || '').toUpperCase();
                const providerMessage = payload.provider_status_detail || payload.provider_status || payload.message || '';
                if (localStatus === 'PAID') {
                  notifyStatus('PAID', '');
                  return;
                }
                if (localStatus === 'PENDING') {
                  notifyStatus('PENDING', providerMessage);
                  return;
                }
                if (localStatus === 'FAILED' || localStatus === 'EXPIRED') {
                  notifyStatus(localStatus, providerMessage || localStatus);
                  throw new Error(providerMessage || localStatus);
                }

                notifyError('Resposta do servidor não reconhecida para cartão.');
                throw new Error('Resposta do servidor não reconhecida para cartão.');
              }

              async function initBrick() {
                if (!window.MercadoPago) {
                  notifyError('SDK do Mercado Pago indisponível.');
                  return;
                }
                const mp = new window.MercadoPago(publicKey, { locale: 'pt-BR' });
                const bricksBuilder = mp.bricks();
                await bricksBuilder.create('cardPayment', 'cardPaymentBrick_container', {
                  initialization: { amount },
                  callbacks: {
                    onReady: () => {},
                    onSubmit: submitPayment,
                    onError: (error) => notifyError(error && error.message ? error.message : 'Falha ao carregar o formulário de cartão.'),
                  },
                });
              }

              initBrick().catch((error) => notifyError(error && error.message ? error.message : 'Falha ao iniciar o formulário de cartão.'));
            </script>
          </body>
        </html>
    """.trimIndent()
}
