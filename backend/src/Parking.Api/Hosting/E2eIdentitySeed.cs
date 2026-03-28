using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Security;

namespace Parking.Api.Hosting;

/// <summary>
/// Quando <c>E2E_SEED=1</c>, garante super admin para Playwright/CI (nunca em produção sem esse flag).
/// </summary>
public static class E2eIdentitySeed
{
    public static async Task EnsureAsync(IdentityDbContext identity, CancellationToken ct = default)
    {
        if (await identity.Users.AnyAsync(u => u.Email == "super@test.com", ct))
            return;

        identity.Users.Add(new ParkingIdentityUser
        {
            Id = Guid.NewGuid(),
            Email = "super@test.com",
            PasswordHash = Argon2PasswordHasher.Hash("Super!12345"),
            Role = UserRole.SUPER_ADMIN,
            ParkingId = null,
            EntityId = null,
            Active = true,
            OperatorSuspended = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await identity.SaveChangesAsync(ct);
    }
}
