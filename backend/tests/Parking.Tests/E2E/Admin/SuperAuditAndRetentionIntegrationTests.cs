using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Api.Hosting;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Audit;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Tenants;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Admin;

/// <summary>SPEC §4 audit read; retenção idempotency/webhook/audit.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class SuperAuditAndRetentionIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task GET_audit_events_sem_JWT_401()
    {
        var http = fx.Factory.CreateClient();
        var r = await http.GetAsync("/api/v1/admin/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task GET_audit_events_admin_tenant_403()
    {
        var http = fx.Factory.CreateClient();
        var (_, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/audit-events");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTok);
        var r = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task GET_audit_events_super_retorna_evento_apos_ticket()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t1.Headers.Add("X-Parking-Id", park);
            t1.Headers.Authorization = auth;
            t1.Content = JsonContent.Create(new { plate = "AUD1T99" });
            var tr = await http.SendAsync(t1);
            tr.EnsureSuccessStatusCode();
        }

        var loginSuper = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        loginSuper.EnsureSuccessStatusCode();
        var superTok = (await loginSuper.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using var gr = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/audit-events?parking_id={parkingId}&limit=20");
        gr.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var ar = await http.SendAsync(gr);
        ar.EnsureSuccessStatusCode();
        var doc = await ar.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        var found = false;
        foreach (var el in items.EnumerateArray())
        {
            if (el.GetProperty("action").GetString() == "TICKET_CREATE")
            {
                found = true;
                break;
            }
        }

        Assert.True(found);
    }

    [Fact]
    public async Task DataRetentionRunner_remove_idempotency_mais_de_24h()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var idemKey = $"k-old-{Guid.NewGuid():N}";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.IdempotencyStore.Add(new IdempotencyStoreRow
            {
                Key = idemKey,
                Route = "/r",
                ResponseJson = "{}",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-25)
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var runner = scope.ServiceProvider.GetRequiredService<DataRetentionRunner>();
            await runner.RunForAllTenantsAsync(identity, CancellationToken.None);
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var any = await db.IdempotencyStore.AnyAsync(x => x.Key == idemKey);
            Assert.False(any);
        }
    }

    [Fact]
    public async Task AuditRetentionRunner_remove_eventos_mais_de_365_dias()
    {
        await using var scope = fx.Factory.Services.CreateAsyncScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var oldId = Guid.NewGuid();
        audit.AuditEvents.Add(new AuditEventRow
        {
            Id = oldId,
            ParkingId = Guid.NewGuid(),
            EntityType = "test",
            EntityId = Guid.NewGuid(),
            Action = "TEST",
            Payload = "{}",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-400)
        });
        await audit.SaveChangesAsync();

        var runner = scope.ServiceProvider.GetRequiredService<AuditRetentionRunner>();
        await runner.PurgeOlderThan365DaysAsync(CancellationToken.None);

        var gone = !await audit.AuditEvents.AnyAsync(e => e.Id == oldId);
        Assert.True(gone);
    }

    [Fact]
    public async Task Cash_close_divergencia_maior_5pct_insere_CASH_DIVERGENCE()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        Guid sessionId;
        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/open"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
            sessionId = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("session_id").GetGuid();
        }

        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var s = await db.CashSessions.FirstAsync(x => x.Id == sessionId);
            s.ExpectedAmount = 100m;
            await db.SaveChangesAsync();
        }

        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/close"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            req.Content = JsonContent.Create(new { sessionId, actualAmount = 90m });
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(j.GetProperty("alert").GetBoolean());
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var a = await db.Alerts.AsNoTracking().FirstOrDefaultAsync(x => x.Type == "CASH_DIVERGENCE");
            Assert.NotNull(a);
            Assert.Contains("expected_amount", a!.Payload, StringComparison.Ordinal);
        }
    }
}
