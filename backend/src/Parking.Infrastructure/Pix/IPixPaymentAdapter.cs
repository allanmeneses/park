namespace Parking.Infrastructure.Pix;

public sealed record PixChargeResult(string QrCode, DateTimeOffset ExpiresAt, string? ProviderTransactionId);

public interface IPixPaymentAdapter
{
    Task<PixChargeResult> CreateChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct);
}
