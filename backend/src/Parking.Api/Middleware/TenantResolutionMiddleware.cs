using System.Security.Claims;
using Parking.Domain;
using Parking.Api.Parking;
using Parking.Infrastructure.Tenants;

namespace Parking.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> SkipPrefixes =
    [
        "/api/v1/auth",
        "/health"
    ];

    public async Task InvokeAsync(
        HttpContext context,
        IConfiguration configuration,
        ILogger<TenantResolutionMiddleware> log)
    {
        var path = (context.Request.Path.Value ?? "").TrimEnd('/');
        if (path.StartsWith("/api/v1/payments/webhook", StringComparison.OrdinalIgnoreCase))
        {
            if (!context.Request.Headers.TryGetValue("X-Parking-Id", out var wh) ||
                !Guid.TryParse(wh.ToString(), out var wp))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { code = "VALIDATION_ERROR", message = "X-Parking-Id obrigatório para rotear o webhook ao tenant." });
                return;
            }

            var tenantTemplate = configuration["TENANT_DATABASE_URL_TEMPLATE"]
                           ?? throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE is required");
            context.Items[ParkingConstants.TenantConnectionStringItem] =
                TenantConnectionStringBuilder.FromTemplate(tenantTemplate, wp);
            context.Items[ParkingConstants.ParkingIdItem] = wp;

            await next(context);
            return;
        }

        if (SkipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method) &&
            path.Equals("/api/v1/admin/tenants", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // SPEC §4 — audit em parking_audit; não resolve tenant (evita exigir X-Parking-Id só para DB global).
        if (HttpMethods.IsGet(context.Request.Method) &&
            path.StartsWith("/api/v1/admin/audit-events", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        Guid? parkingId = null;
        if (string.Equals(role, nameof(UserRole.SUPER_ADMIN), StringComparison.OrdinalIgnoreCase))
        {
            if (!context.Request.Headers.TryGetValue("X-Parking-Id", out var hid) ||
                !Guid.TryParse(hid.ToString(), out var pid))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { code = "VALIDATION_ERROR", message = "X-Parking-Id obrigatório para SUPER_ADMIN." });
                return;
            }

            parkingId = pid;
        }
        else
        {
            var claim = context.User.FindFirst("parking_id")?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var pid))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { code = "FORBIDDEN", message = "parking_id ausente no token." });
                return;
            }

            parkingId = pid;
        }

        var template = configuration["TENANT_DATABASE_URL_TEMPLATE"]
                       ?? throw new InvalidOperationException("TENANT_DATABASE_URL_TEMPLATE is required");
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId.Value);
        context.Items[ParkingConstants.TenantConnectionStringItem] = cs;
        context.Items[ParkingConstants.ParkingIdItem] = parkingId.Value;

        try
        {
            await using var probe = new Npgsql.NpgsqlConnection(cs);
            await probe.OpenAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Tenant DB unavailable for {ParkingId}", parkingId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { code = "TENANT_UNAVAILABLE", message = "Banco do tenant indisponível." });
            return;
        }

        await next(context);
    }
}
