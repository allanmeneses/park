using System.Text.Json;
using Parking.Infrastructure.Persistence.Audit;

namespace Parking.Infrastructure.Audit;

public sealed class AuditService(AuditDbContext audit)
{
    public async Task AppendAsync(
        Guid parkingId,
        string entityType,
        Guid entityId,
        string action,
        object payload,
        CancellationToken ct)
    {
        audit.AuditEvents.Add(new AuditEventRow
        {
            Id = Guid.NewGuid(),
            ParkingId = parkingId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Payload = JsonSerializer.Serialize(payload),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await audit.SaveChangesAsync(ct);
    }
}
