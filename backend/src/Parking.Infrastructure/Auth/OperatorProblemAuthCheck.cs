using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Tenants;

namespace Parking.Infrastructure.Auth;

/// <summary>SPEC §7 — operador com mais de 3 eventos PROBLEM no dia UTC deve receber OPERATOR_BLOCKED no login/refresh.</summary>
public interface IOperatorProblemAuthCheck
{
    Task<bool> ExceedsDailyProblemLimitAsync(ParkingIdentityUser user, CancellationToken ct);
}

public sealed class OperatorProblemAuthCheck(
    ITenantDbContextFactory tenantFactory,
    IConfiguration configuration) : IOperatorProblemAuthCheck
{
    public async Task<bool> ExceedsDailyProblemLimitAsync(ParkingIdentityUser user, CancellationToken ct)
    {
        if (user.Role != UserRole.OPERATOR || user.ParkingId is not { } pid)
            return false;

        var template = configuration["TENANT_DATABASE_URL_TEMPLATE"]
                       ?? throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE is required");
        var cs = TenantConnectionStringBuilder.FromTemplate(template, pid);
        await using var db = tenantFactory.CreateReadWrite(cs);
        var now = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var count = await db.OperatorEvents.AsNoTracking()
            .CountAsync(
                e => e.UserId == user.Id && e.Type == "PROBLEM" && e.CreatedAt >= dayStart && e.CreatedAt < dayEnd,
                ct);
        return count > 3;
    }
}
