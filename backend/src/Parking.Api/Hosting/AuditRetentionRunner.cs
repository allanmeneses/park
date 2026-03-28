using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.Persistence.Audit;

namespace Parking.Api.Hosting;

/// <summary>SPEC §4 — apagar audit_events com created_at &lt; 365 dias (job diário).</summary>
public sealed class AuditRetentionRunner(AuditDbContext audit)
{
    public async Task PurgeOlderThan365DaysAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-365);
        await audit.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM audit_events WHERE created_at < {cutoff}""",
            cancellationToken: ct);
    }
}
