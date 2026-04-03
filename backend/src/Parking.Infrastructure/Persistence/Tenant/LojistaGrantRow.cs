using System.ComponentModel.DataAnnotations.Schema;

namespace Parking.Infrastructure.Persistence.Tenant;

/// <summary>Bonus transfer from lojista wallet to client wallet.</summary>
[Table("lojista_grants")]
public sealed class LojistaGrantRow
{
    public Guid Id { get; set; }
    public Guid LojistaId { get; set; }
    public Guid ClientId { get; set; }

    /// <summary>Normalized plate at grant time.</summary>
    public string Plate { get; set; } = "";

    public int Hours { get; set; }

    /// <summary>Grant mode: ADVANCE or ON_SITE.</summary>
    public string GrantMode { get; set; } = "ADVANCE";

    public DateTimeOffset CreatedAt { get; set; }
}
