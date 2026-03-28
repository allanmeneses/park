using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Tenants;

namespace Parking.Api.Hosting;

/// <summary>SPEC §12 — expira PIX PENDING com transação ativa vencida.</summary>
public sealed class PixExpiryRunner(ITenantDbContextFactory tenantFactory, IConfiguration configuration)
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

        foreach (var parkingId in parkingIds)
        {
            var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
            await using var db = tenantFactory.CreateReadWrite(cs);
            await ExpireInTenantAsync(db, ct);
        }
    }

    private static async Task ExpireInTenantAsync(TenantDbContext db, CancellationToken ct)
    {
        var utc = DateTimeOffset.UtcNow;
        var stale = await db.PixTransactions
            .Where(x => x.Active && x.ExpiresAt < utc)
            .ToListAsync(ct);
        if (stale.Count == 0) return;

        foreach (var grp in stale.GroupBy(x => x.PaymentId))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == grp.Key, ct);
            if (payment is not { Status: PaymentStatus.PENDING })
            {
                await tx.RollbackAsync(ct);
                continue;
            }

            if (payment.Method is not null && payment.Method != PaymentMethod.PIX)
            {
                await tx.RollbackAsync(ct);
                continue;
            }

            foreach (var px in grp)
                px.Active = false;

            payment.Status = PaymentStatus.EXPIRED;
            payment.FailedReason = "PIX_EXPIRED";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}
