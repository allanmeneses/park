namespace Parking.Infrastructure.Pix;

/// <summary>SPEC §9.1 Stub — EMV-like string mín. 32 chars.</summary>
public sealed class StubPixProvider : IPixPaymentAdapter
{
    public Task<PixChargeResult> CreateChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct)
    {
        var providerTx = Guid.NewGuid().ToString();
        var basePayload = $"PIXSTUB|{paymentId:N}|{amount:0.00}|{providerTx}";
        var qr = basePayload.Length >= 32
            ? "00020126" + basePayload
            : ("00020126" + basePayload + new string('0', 32 - basePayload.Length));
        var exp = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
        return Task.FromResult(new PixChargeResult(qr, exp, providerTx));
    }
}
