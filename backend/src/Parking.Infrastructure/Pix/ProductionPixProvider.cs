namespace Parking.Infrastructure.Pix;

/// <summary>Placeholder: credenciais obrigatórias em PIX_MODE=Production (SPEC §9.1).</summary>
public sealed class ProductionPixProvider : IPixPaymentAdapter
{
    public ProductionPixProvider()
    {
        var clientId = Environment.GetEnvironmentVariable("PIX_PSP_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("PIX_PSP_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(
                "PIX_MODE=Production exige PIX_PSP_CLIENT_ID e PIX_PSP_CLIENT_SECRET definidos.");
    }

    public Task<PixChargeResult> CreateChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct) =>
        throw new NotImplementedException("Adaptador PSP real não implementado neste repositório.");
}
