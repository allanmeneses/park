using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Tenants;

namespace Parking.Api.Hosting;

/// <summary>SPEC tenant DDL — idempotency_store &gt; 24h; webhook_receipts &gt; 30 dias.</summary>
public sealed class DataRetentionRunner(ITenantDbContextFactory tenantFactory, IConfiguration configuration)
{
    public async Task RunForAllTenantsAsync(IdentityDbContext identity, CancellationToken ct)
    {
        var template = configuration["TENANT_DATABASE_URL_TEMPLATE"]
                       ?? throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE is required");
        var parkingIds = await identity.Users.AsNoTracking()
            .Where(u => u.ParkingId != null)
            .Select(u => u.ParkingId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var idemCutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var webhookCutoff = DateTimeOffset.UtcNow.AddDays(-30);

        foreach (var parkingId in parkingIds)
        {
            var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
            await using var db = tenantFactory.CreateReadWrite(cs);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM idempotency_store WHERE created_at < {idemCutoff}""",
                cancellationToken: ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""DELETE FROM webhook_receipts WHERE processed_at < {webhookCutoff}""",
                cancellationToken: ct);
        }
    }
}
