using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Payments;

/// <summary>Resolve <see cref="MercadoPagoOptions"/> efetivos: credenciais do tenant ou fallback global.</summary>
public sealed class MercadoPagoTenantOptionsResolver(
    IConfiguration configuration,
    IOptions<MercadoPagoOptions> globalOptions,
    TenantSecretProtector protector)
{
    private static readonly Guid SingletonRowId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Mercado Pago efetivo para pedidos HTTP e webhooks (tenant ou global).</summary>
    public async Task<MercadoPagoOptions> ResolveEffectiveAsync(TenantDbContext db, CancellationToken ct)
    {
        var global = globalOptions.Value.Clone();
        if (!PaymentModeHelper.IsGlobalMercadoPago(configuration))
            return global;

        var row = await db.PspMercadoPagoSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == SingletonRowId, ct);

        if (row is not { UseTenantCredentials: true } ||
            string.IsNullOrWhiteSpace(row.AccessTokenCipher) ||
            string.IsNullOrWhiteSpace(row.WebhookSecretCipher))
            return global;

        if (!protector.IsConfigured)
            return global;

        try
        {
            var access = protector.Unprotect(row.AccessTokenCipher);
            var webhook = protector.Unprotect(row.WebhookSecretCipher);
            return new MercadoPagoOptions
            {
                AccessToken = access,
                WebhookSecret = webhook,
                PublicKey = string.IsNullOrWhiteSpace(row.PublicKey) ? global.PublicKey : row.PublicKey,
                PayerEmail = string.IsNullOrWhiteSpace(row.PayerEmail) ? global.PayerEmail : row.PayerEmail,
                ApiBaseUrl = string.IsNullOrWhiteSpace(row.ApiBaseUrl) ? global.ApiBaseUrl : row.ApiBaseUrl!.Trim(),
                CheckoutBackSuccessUrl = row.CheckoutBackSuccessUrl ?? global.CheckoutBackSuccessUrl,
                CheckoutBackFailureUrl = row.CheckoutBackFailureUrl ?? global.CheckoutBackFailureUrl,
                CheckoutBackPendingUrl = row.CheckoutBackPendingUrl ?? global.CheckoutBackPendingUrl
            };
        }
        catch
        {
            return global;
        }
    }
}
