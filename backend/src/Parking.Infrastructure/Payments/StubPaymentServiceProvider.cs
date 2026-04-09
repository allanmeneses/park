namespace Parking.Infrastructure.Payments;

/// <summary>SPEC §9.1 Stub — Pix simulado; cartão com confirmação síncrona na API (sem PSP).</summary>
public sealed class StubPaymentServiceProvider : IPaymentServiceProvider
{
    public string ProviderId => "stub";

    public CardPaymentFlow CardFlow => CardPaymentFlow.InPersonSimulated;

    public Task<PixChargeResult> CreatePixChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct)
    {
        var providerTx = Guid.NewGuid().ToString();
        var basePayload = $"PIXSTUB|{paymentId:N}|{amount:0.00}|{providerTx}";
        var qr = basePayload.Length >= 32
            ? "00020126" + basePayload
            : "00020126" + basePayload + new string('0', 32 - basePayload.Length);
        var exp = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        return Task.FromResult(new PixChargeResult(qr, exp, providerTx));
    }

    public Task<CardCheckoutSession> CreateCardCheckoutAsync(Guid paymentId, decimal amount, CancellationToken ct) =>
        throw new NotSupportedException("Stub não usa checkout hospedado; use POST /payments/card com fluxo síncrono.");
}
