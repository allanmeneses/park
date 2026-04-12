using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Parking.Infrastructure.Payments.MercadoPago;

public sealed class MercadoPagoPaymentServiceProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<MercadoPagoOptions> options) : IPaymentServiceProvider
{
    private readonly MercadoPagoOptions _opt = options.Value;

    public string ProviderId => "mercadopago";

    public CardPaymentFlow CardFlow => CardPaymentFlow.HostedCheckout;

    public bool SupportsEmbeddedCardPayments => true;

    public async Task<string?> FetchProviderPaymentJsonAsync(string providerPaymentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
            return null;
        var id = providerPaymentId.Trim();
        if (id.Length == 0)
            return null;
        var client = httpClientFactory.CreateClient(nameof(MercadoPagoPaymentServiceProvider));
        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/{id}");
        using var res = await client.SendAsync(get, ct);
        var txt = await res.Content.ReadAsStringAsync(ct);
        return res.IsSuccessStatusCode ? txt : null;
    }

    public async Task<PixChargeResult> CreatePixChargeAsync(
        Guid paymentId,
        decimal amount,
        int expiresInSeconds,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
            throw new InvalidOperationException("MercadoPago:AccessToken (ou MERCADOPAGO_ACCESS_TOKEN) é obrigatório.");

        var client = httpClientFactory.CreateClient(nameof(MercadoPagoPaymentServiceProvider));
        var expiration = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = amount,
            ["description"] = "Estacionamento",
            ["payment_method_id"] = "pix",
            ["external_reference"] = paymentId.ToString("D"),
            ["payer"] = new Dictionary<string, string> { ["email"] = _opt.PayerEmail },
            ["date_of_expiration"] = expiration.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture)
        };

        var json = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/payments")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        // Obrigatório na API MP (senão 400 "X-Idempotency-Key can't be null").
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var res = await client.SendAsync(req, ct);
        var responseText = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago Pix falhou: {(int)res.StatusCode} {responseText}");

        return MercadoPagoApiParser.ParsePixPaymentResponse(responseText, paymentId);
    }

    public async Task<CardCheckoutSession> CreateCardCheckoutAsync(Guid paymentId, decimal amount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
            throw new InvalidOperationException("MercadoPago:AccessToken (ou MERCADOPAGO_ACCESS_TOKEN) é obrigatório.");

        var client = httpClientFactory.CreateClient(nameof(MercadoPagoPaymentServiceProvider));
        var item = new Dictionary<string, object?>
        {
            ["title"] = "Estacionamento",
            ["quantity"] = 1,
            ["currency_id"] = "BRL",
            ["unit_price"] = amount
        };

        var pref = new Dictionary<string, object?>
        {
            ["items"] = new[] { item },
            ["external_reference"] = paymentId.ToString("D"),
            ["binary_mode"] = true
        };

        if (!string.IsNullOrWhiteSpace(_opt.CheckoutBackSuccessUrl) &&
            !string.IsNullOrWhiteSpace(_opt.CheckoutBackFailureUrl) &&
            !string.IsNullOrWhiteSpace(_opt.CheckoutBackPendingUrl))
        {
            pref["back_urls"] = new Dictionary<string, string?>
            {
                ["success"] = _opt.CheckoutBackSuccessUrl,
                ["failure"] = _opt.CheckoutBackFailureUrl,
                ["pending"] = _opt.CheckoutBackPendingUrl
            };
        }

        var json = JsonSerializer.Serialize(pref);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/checkout/preferences")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var res = await client.SendAsync(req, ct);
        var responseText = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago Preference falhou: {(int)res.StatusCode} {responseText}");

        return MercadoPagoApiParser.ParsePreferenceResponse(responseText, _opt.PublicKey);
    }

    public Task<EmbeddedCardSession> CreateEmbeddedCardSessionAsync(Guid paymentId, decimal amount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.PublicKey))
            throw new InvalidOperationException("MercadoPago:PublicKey (ou MERCADOPAGO_PUBLIC_KEY) é obrigatório.");
        return Task.FromResult(new EmbeddedCardSession(_opt.PublicKey));
    }

    public async Task<EmbeddedCardPaymentResult> SubmitEmbeddedCardPaymentAsync(EmbeddedCardPaymentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.AccessToken))
            throw new InvalidOperationException("MercadoPago:AccessToken (ou MERCADOPAGO_ACCESS_TOKEN) é obrigatório.");

        var client = httpClientFactory.CreateClient(nameof(MercadoPagoPaymentServiceProvider));

        object? issuerId = null;
        if (!string.IsNullOrWhiteSpace(request.IssuerId))
            issuerId = long.TryParse(request.IssuerId, out var issuerNumeric) ? issuerNumeric : request.IssuerId.Trim();

        var payer = new Dictionary<string, object?>
        {
            ["email"] = request.PayerEmail.Trim(),
        };
        if (!string.IsNullOrWhiteSpace(request.IdentificationType) && !string.IsNullOrWhiteSpace(request.IdentificationNumber))
        {
            payer["identification"] = new Dictionary<string, object?>
            {
                ["type"] = request.IdentificationType.Trim(),
                ["number"] = request.IdentificationNumber.Trim(),
            };
        }

        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = request.Amount,
            ["token"] = request.Token.Trim(),
            ["description"] = "Compra de créditos do estacionamento",
            ["installments"] = request.Installments,
            ["payment_method_id"] = request.PaymentMethodId.Trim(),
            ["external_reference"] = request.PaymentId.ToString("D"),
            ["binary_mode"] = true,
            ["payer"] = payer,
        };
        if (issuerId is not null)
            body["issuer_id"] = issuerId;

        var json = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/payments")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var res = await client.SendAsync(req, ct);
        var responseText = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago cartão embutido falhou: {(int)res.StatusCode} {responseText}");

        return MercadoPagoApiParser.ParseEmbeddedCardPaymentResponse(responseText);
    }
}
