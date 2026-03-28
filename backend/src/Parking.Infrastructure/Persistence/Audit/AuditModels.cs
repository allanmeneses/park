using System.ComponentModel.DataAnnotations.Schema;

namespace Parking.Infrastructure.Persistence.Audit;

[Table("audit_events")]
public class AuditEventRow
{
    public Guid Id { get; set; }
    public Guid ParkingId { get; set; }
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
