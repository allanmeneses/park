using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Parking.Infrastructure.Payments;
using Parking.Infrastructure.Payments.MercadoPago;

namespace Parking.Tests.Unit;

public sealed class MercadoPagoPaymentServiceProviderTests
{
    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            const string json = """
                {"id":999001,"date_of_expiration":"2030-01-01T00:00:00.000-04:00","point_of_interaction":{"transaction_data":{"qr_code":"qr-test"}}}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    [Fact]
    public async Task CreatePixChargeAsync_envia_X_Idempotency_Key()
    {
        var capture = new CaptureHandler();
        var client = new HttpClient(capture) { BaseAddress = new Uri("https://api.mercadopago.com/", UriKind.Absolute) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "TEST-token");
        var factory = new FakeClientFactory(client);
        var opt = Options.Create(new MercadoPagoOptions { AccessToken = "TEST-token", PayerEmail = "payer@test.com" });
        var provider = new MercadoPagoPaymentServiceProvider(factory, opt);

        var r = await provider.CreatePixChargeAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"), 5m, 300, default);

        Assert.NotNull(capture.Last);
        Assert.True(capture.Last!.Headers.TryGetValues("X-Idempotency-Key", out var keys));
        Assert.NotEmpty(keys.First());
        Assert.Equal("qr-test", r.QrCode);
    }
}
