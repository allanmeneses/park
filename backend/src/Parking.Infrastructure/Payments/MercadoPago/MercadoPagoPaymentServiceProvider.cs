using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Parking.Infrastructure.Payments.MercadoPago;

public sealed class MercadoPagoPaymentServiceProvider : IPaymentServiceProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MercadoPagoOptions _opt;

    public MercadoPagoPaymentServiceProvider(IHttpClientFactory httpClientFactory, IOptions<MercadoPagoOptions> options)
        : this(httpClientFactory, options.Value)
    {
    }

    public MercadoPagoPaymentServiceProvider(IHttpClientFactory httpClientFactory, MercadoPagoOptions opt)
    {
        _httpClientFactory = httpClientFactory;
        _opt = opt;
    }

    private Uri ApiRootUri()
    {
        var b = string.IsNullOrWhiteSpace(_opt.ApiBaseUrl) ? "https://api.mercadopago.com" : _opt.ApiBaseUrl.TrimEnd('/');
        return new Uri(b + "/", UriKind.Absolute);
    }

    private HttpClient CreateHttpClient() => _httpClientFactory.CreateClient(nameof(MercadoPagoPaymentServiceProvider));

    private static void AddBearer(HttpRequestMessage req, MercadoPagoOptions opt)
    {
        if (!string.IsNullOrWhiteSpace(opt.AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opt.AccessToken.Trim());
    }

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
        var uri = new Uri(ApiRootUri(), "v1/payments/" + Uri.EscapeDataString(id));
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        AddBearer(req, _opt);
        var client = CreateHttpClient();
        using var res = await client.SendAsync(req, ct);
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
        var uri = new Uri(ApiRootUri(), "v1/payments");
        using var req = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddBearer(req, _opt);
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        var client = CreateHttpClient();
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
        var uri = new Uri(ApiRootUri(), "checkout/preferences");
        using var req = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddBearer(req, _opt);
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        var client = CreateHttpClient();
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
        var uri = new Uri(ApiRootUri(), "v1/payments");
        using var req = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddBearer(req, _opt);
        req.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString("N"));
        var client = CreateHttpClient();
        using var res = await client.SendAsync(req, ct);
        var responseText = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mercado Pago cartão embutido falhou: {(int)res.StatusCode} {responseText}");

        return MercadoPagoApiParser.ParseEmbeddedCardPaymentResponse(responseText);
    }
}
