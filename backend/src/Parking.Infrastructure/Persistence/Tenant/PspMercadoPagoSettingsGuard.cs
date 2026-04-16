using Microsoft.EntityFrameworkCore;

namespace Parking.Infrastructure.Persistence.Tenant;

/// <summary>Garante linha singleton de PSP Mercado Pago por tenant.</summary>
public static class PspMercadoPagoSettingsGuard
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task EnsureAsync(TenantDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO psp_mercado_pago_settings (
              id, use_tenant_credentials, environment, access_token_cipher, webhook_secret_cipher,
              public_key, payer_email, updated_at)
            VALUES (
              '00000000-0000-0000-0000-000000000001'::uuid,
              false, 'PRODUCTION', '', '', '', '', NOW() AT TIME ZONE 'UTC')
            ON CONFLICT (id) DO NOTHING
            """,
            cancellationToken: ct);
    }
}
