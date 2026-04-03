using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Parking.Api.Middleware;
using Parking.Api.Parking;
using Parking.Infrastructure;
using Parking.Infrastructure.Persistence.Audit;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Tenants;
using Parking.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddParkingInfrastructure(builder.Configuration);
builder.Services.AddScoped<PixExpiryRunner>();
builder.Services.AddScoped<DataRetentionRunner>();
builder.Services.AddScoped<AuditRetentionRunner>();
builder.Services.AddHostedService<PixExpiryBackgroundService>();
builder.Services.AddHostedService<DataRetentionBackgroundService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantDbContext>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (http == null)
        throw new InvalidOperationException("HttpContext required.");
    var cs = http.Items[ParkingConstants.TenantConnectionStringItem] as string;
    if (string.IsNullOrEmpty(cs))
        throw new InvalidOperationException("Tenant connection not resolved for this request.");
    return sp.GetRequiredService<ITenantDbContextFactory>().CreateReadWrite(cs);
});

var jwtSecret = builder.Configuration["JWT_SECRET"]
                ?? builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("JWT_SECRET or Jwt:Secret is required (min 32 chars).");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("JWT secret must be at least 32 characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "parking-identity",
            ValidateAudience = true,
            ValidAudience = "parking-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(120),
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

var cors = builder.Configuration["CORS_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(cors).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
    if (string.Equals(app.Configuration["E2E_SEED"], "1", StringComparison.Ordinal))
        await E2eIdentitySeed.EnsureAsync(sp.GetRequiredService<IdentityDbContext>());

    // Tenants: novas migrations (ex. lojista_grants) devem aplicar-se a BD já existentes — antes só corriam no provisionamento.
    var tenantTemplate = app.Configuration["TENANT_DATABASE_URL_TEMPLATE"];
    if (!string.IsNullOrWhiteSpace(tenantTemplate))
    {
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("TenantDbStartup");
        var identityDb = sp.GetRequiredService<IdentityDbContext>();
        var tenantFactory = sp.GetRequiredService<ITenantDbContextFactory>();
        var fromUsers = await identityDb.Users.AsNoTracking()
            .Where(u => u.ParkingId != null)
            .Select(u => u.ParkingId!.Value)
            .ToListAsync();
        var fromInvites = await identityDb.LojistaInvites.AsNoTracking()
            .Select(i => i.ParkingId)
            .ToListAsync();
        foreach (var parkingId in fromUsers.Concat(fromInvites).Distinct())
        {
            try
            {
                var cs = TenantConnectionStringBuilder.FromTemplate(tenantTemplate, parkingId);
                await using var tctx = tenantFactory.CreateReadWrite(cs);
                await tctx.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Tenant DB migrate failed for parking_id {ParkingId}", parkingId);
            }
        }
    }
}

app.Use(async (ctx, next) =>
{
    ctx.Request.EnableBuffering();
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantResolutionMiddleware>();
app.MapControllers();

app.MapGet("/health", () => Results.Json(new { ok = true })).AllowAnonymous();

app.Run();

public partial class Program;
