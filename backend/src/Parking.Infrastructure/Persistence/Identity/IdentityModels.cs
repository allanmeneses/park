using System.ComponentModel.DataAnnotations.Schema;
using Parking.Domain;

namespace Parking.Infrastructure.Persistence.Identity;

[Table("users")]
public class ParkingIdentityUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public Guid? ParkingId { get; set; }
    public Guid? EntityId { get; set; }
    public bool Active { get; set; } = true;
    public bool OperatorSuspended { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

[Table("refresh_tokens")]
public class RefreshTokenRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
