using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Parking.Infrastructure.Payments.MercadoPago;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Payments;

/// <summary>
/// Delega para <see cref="StubPaymentServiceProvider"/> (PSP global stub) ou
/// <see cref="MercadoPagoPaymentServiceProvider"/> com opções efetivas (tenant ou global).
/// </summary>
public sealed class TenantScopedPaymentServiceProvider(
    TenantDbContext db,
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    MercadoPagoTenantOptionsResolver mpResolver,
    StubPaymentServiceProvider stubProvider) : IPaymentServiceProvider
{
    private MercadoPagoPaymentServiceProvider? _cachedMp;

    public string ProviderId => PaymentModeHelper.IsGlobalMercadoPago(configuration) ? "mercadopago" : "stub";

    public CardPaymentFlow CardFlow =>
        PaymentModeHelper.IsGlobalMercadoPago(configuration) ? CardPaymentFlow.HostedCheckout : CardPaymentFlow.InPersonSimulated;

    /// <summary>Stub e Mercado Pago suportam o fluxo embutido neste produto (stub simulado; MP via Bricks).</summary>
    public bool SupportsEmbeddedCardPayments => true;

    private async Task<MercadoPagoPaymentServiceProvider> GetMercadoPagoAsync(CancellationToken ct)
    {
        if (_cachedMp != null)
            return _cachedMp;
        var opts = await mpResolver.ResolveEffectiveAsync(db, ct);
        _cachedMp = new MercadoPagoPaymentServiceProvider(httpClientFactory, opts);
        return _cachedMp;
    }

    public async Task<PixChargeResult> CreatePixChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct)
    {
        if (!PaymentModeHelper.IsGlobalMercadoPago(configuration))
            return await stubProvider.CreatePixChargeAsync(paymentId, amount, expiresInSeconds, ct);
        var mp = await GetMercadoPagoAsync(ct);
        return await mp.CreatePixChargeAsync(paymentId, amount, expiresInSeconds, ct);
    }

    public async Task<string?> FetchProviderPaymentJsonAsync(string providerPaymentId, CancellationToken ct)
    {
        if (!PaymentModeHelper.IsGlobalMercadoPago(configuration))
            return await stubProvider.FetchProviderPaymentJsonAsync(providerPaymentId, ct);
        var mp = await GetMercadoPagoAsync(ct);
        return await mp.FetchProviderPaymentJsonAsync(providerPaymentId, ct);
    }

    public async Task<CardCheckoutSession> CreateCardCheckoutAsync(Guid paymentId, decimal amount, CancellationToken ct)
    {
        if (!PaymentModeHelper.IsGlobalMercadoPago(configuration))
            return await stubProvider.CreateCardCheckoutAsync(paymentId, amount, ct);
        var mp = await GetMercadoPagoAsync(ct);
        return await mp.CreateCardCheckoutAsync(paymentId, amount, ct);
    }

    public async Task<EmbeddedCardSession> CreateEmbeddedCardSessionAsync(Guid paymentId, decimal amount, CancellationToken ct)
    {
        if (!PaymentModeHelper.IsGlobalMercadoPago(configuration))
            return await stubProvider.CreateEmbeddedCardSessionAsync(paymentId, amount, ct);
        var mp = await GetMercadoPagoAsync(ct);
        return await mp.CreateEmbeddedCardSessionAsync(paymentId, amount, ct);
    }

    public async Task<EmbeddedCardPaymentResult> SubmitEmbeddedCardPaymentAsync(EmbeddedCardPaymentRequest request, CancellationToken ct)
    {
        if (!PaymentModeHelper.IsGlobalMercadoPago(configuration))
            return await stubProvider.SubmitEmbeddedCardPaymentAsync(request, ct);
        var mp = await GetMercadoPagoAsync(ct);
        return await mp.SubmitEmbeddedCardPaymentAsync(request, ct);
    }
}
