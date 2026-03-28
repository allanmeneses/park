using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Audit;

namespace Parking.Api.Controllers;

/// <summary>SPEC §4 — leitura de auditoria apenas SUPER_ADMIN (base global parking_audit).</summary>
[ApiController]
[Route("api/v1/admin/audit-events")]
[Authorize(Roles = nameof(UserRole.SUPER_ADMIN))]
public sealed class SuperAuditController(AuditDbContext audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery(Name = "parking_id")] Guid? parkingId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var q = audit.AuditEvents.AsNoTracking().AsQueryable();
        if (parkingId is { } pid)
            q = q.Where(e => e.ParkingId == pid);
        var items = await q
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new
            {
                id = e.Id,
                parking_id = e.ParkingId,
                entity_type = e.EntityType,
                entity_id = e.EntityId,
                action = e.Action,
                payload = e.Payload,
                created_at = e.CreatedAt
            })
            .ToListAsync(ct);
        return Ok(new { items });
    }
}
