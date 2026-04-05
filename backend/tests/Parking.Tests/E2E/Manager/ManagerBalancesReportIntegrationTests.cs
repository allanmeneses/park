using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Tenants;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Manager;

[Collection(nameof(PostgresApiCollection))]
public sealed class ManagerBalancesReportIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Balances_report_clients_ordered_by_balance_hours_desc_then_plate()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            db.Clients.AddRange(
                new ClientRow { Id = a, Plate = "AAA1111", LojistaId = null },
                new ClientRow { Id = b, Plate = "BBB2222", LojistaId = null },
                new ClientRow { Id = c, Plate = "CCC3333", LojistaId = null });
            db.ClientWallets.AddRange(
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = a, BalanceHours = 1, ExpirationDate = null },
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = b, BalanceHours = 10, ExpirationDate = null },
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = c, BalanceHours = 3, ExpirationDate = null });
            var l1 = Guid.NewGuid();
            var l2 = Guid.NewGuid();
            db.Lojistas.AddRange(
                new LojistaRow { Id = l1, Name = "Loj A", HourPrice = 1m, AllowGrantBeforeEntry = true },
                new LojistaRow { Id = l2, Name = "Loj B", HourPrice = 1m, AllowGrantBeforeEntry = true });
            db.LojistaWallets.AddRange(
                new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = l1, BalanceHours = 5 },
                new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = l2, BalanceHours = 20 });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manager/balances-report");
        req.Headers.Authorization = auth;
        req.Headers.Add("X-Parking-Id", park);
        var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("clientPlates", out var cp));
        Assert.Equal(3, cp.GetArrayLength());
        Assert.Equal("BBB2222", cp[0].GetProperty("plate").GetString());
        Assert.Equal(10, cp[0].GetProperty("balanceHours").GetInt32());
        Assert.Equal("CCC3333", cp[1].GetProperty("plate").GetString());
        Assert.Equal(3, cp[1].GetProperty("balanceHours").GetInt32());
        Assert.Equal("AAA1111", cp[2].GetProperty("plate").GetString());
        Assert.Equal(1, cp[2].GetProperty("balanceHours").GetInt32());

        Assert.True(body.TryGetProperty("lojistas", out var lj));
        Assert.Equal(2, lj.GetArrayLength());
        Assert.Equal(20, lj[0].GetProperty("balanceHours").GetInt32());
        Assert.Equal(5, lj[1].GetProperty("balanceHours").GetInt32());

        Assert.True(body.TryGetProperty("lojistaBonificadoPlates", out var bon));
        Assert.Equal(0, bon.GetArrayLength());
    }

    [Fact]
    public async Task Balances_report_plate_query_filters_client_plates_substring()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var x = Guid.NewGuid();
            var y = Guid.NewGuid();
            db.Clients.AddRange(
                new ClientRow { Id = x, Plate = "PRA4658", LojistaId = null },
                new ClientRow { Id = y, Plate = "XYZ9K99", LojistaId = null });
            db.ClientWallets.AddRange(
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = x, BalanceHours = 7, ExpirationDate = null },
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = y, BalanceHours = 99, ExpirationDate = null });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manager/balances-report?plate=PRA");
        req.Headers.Authorization = auth;
        req.Headers.Add("X-Parking-Id", park);
        var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var cp = body.GetProperty("clientPlates");
        Assert.Equal(1, cp.GetArrayLength());
        Assert.Equal("PRA4658", cp[0].GetProperty("plate").GetString());
        Assert.Equal(7, cp[0].GetProperty("balanceHours").GetInt32());

        var bon = body.GetProperty("lojistaBonificadoPlates");
        Assert.Equal(0, bon.GetArrayLength());
    }

    [Fact]
    public async Task Balances_report_lojistaBonificadoPlates_lists_positive_bonificado_ordered_by_balance_desc()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var lojId = Guid.NewGuid();
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = "Loj Bon",
                HourPrice = 1m,
                AllowGrantBeforeEntry = true,
            });
            db.LojistaWallets.Add(new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = lojId, BalanceHours = 100 });

            var cHigh = Guid.NewGuid();
            var cLow = Guid.NewGuid();
            db.Clients.AddRange(
                new ClientRow { Id = cHigh, Plate = "HIH9A99", LojistaId = lojId },
                new ClientRow { Id = cLow, Plate = "LOW8B88", LojistaId = lojId });
            db.ClientWallets.AddRange(
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = cHigh, BalanceHours = 0, ExpirationDate = null },
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = cLow, BalanceHours = 0, ExpirationDate = null });

            var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
            db.LojistaGrants.AddRange(
                new LojistaGrantRow
                {
                    Id = Guid.NewGuid(),
                    LojistaId = lojId,
                    ClientId = cHigh,
                    Plate = "HIH9A99",
                    Hours = 7,
                    GrantMode = "ADVANCE",
                    CreatedAt = t0,
                },
                new LojistaGrantRow
                {
                    Id = Guid.NewGuid(),
                    LojistaId = lojId,
                    ClientId = cHigh,
                    Plate = "HIH9A99",
                    Hours = 5,
                    GrantMode = "ADVANCE",
                    CreatedAt = t0.AddSeconds(1),
                },
                new LojistaGrantRow
                {
                    Id = Guid.NewGuid(),
                    LojistaId = lojId,
                    ClientId = cLow,
                    Plate = "LOW8B88",
                    Hours = 4,
                    GrantMode = "ADVANCE",
                    CreatedAt = t0,
                });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manager/balances-report");
        req.Headers.Authorization = auth;
        req.Headers.Add("X-Parking-Id", park);
        var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var bon = body.GetProperty("lojistaBonificadoPlates");
        Assert.Equal(2, bon.GetArrayLength());
        Assert.Equal("HIH9A99", bon[0].GetProperty("plate").GetString());
        Assert.Equal(12, bon[0].GetProperty("balanceHours").GetInt32());
        Assert.Equal("LOW8B88", bon[1].GetProperty("plate").GetString());
        Assert.Equal(4, bon[1].GetProperty("balanceHours").GetInt32());
    }

    [Fact]
    public async Task Balances_report_lojistaBonificadoPlates_omits_plate_when_fully_consumed_after_grant()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var lojId = Guid.NewGuid();
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = "Loj Use",
                HourPrice = 1m,
                AllowGrantBeforeEntry = true,
            });
            db.LojistaWallets.Add(new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = lojId, BalanceHours = 50 });

            var cid = Guid.NewGuid();
            var ticketId = Guid.NewGuid();
            var grantAt = DateTimeOffset.UtcNow.AddHours(-2);
            var exitAt = DateTimeOffset.UtcNow.AddHours(-1);
            db.Clients.Add(new ClientRow { Id = cid, Plate = "USE7C77", LojistaId = lojId });
            db.ClientWallets.Add(new ClientWalletRow
            {
                Id = Guid.NewGuid(),
                ClientId = cid,
                BalanceHours = 0,
                ExpirationDate = null,
            });
            db.LojistaGrants.Add(new LojistaGrantRow
            {
                Id = Guid.NewGuid(),
                LojistaId = lojId,
                ClientId = cid,
                Plate = "USE7C77",
                Hours = 3,
                GrantMode = "ADVANCE",
                CreatedAt = grantAt,
            });
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = "USE7C77",
                EntryTime = grantAt.AddMinutes(-30),
                ExitTime = exitAt,
                Status = TicketStatus.CLOSED,
                CreatedAt = grantAt.AddMinutes(-30),
            });
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                Source = "lojista",
                HoursUsed = 3,
            });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manager/balances-report");
        req.Headers.Authorization = auth;
        req.Headers.Add("X-Parking-Id", park);
        var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var bon = body.GetProperty("lojistaBonificadoPlates");
        Assert.Equal(0, bon.GetArrayLength());
    }

    [Fact]
    public async Task Balances_report_expired_client_wallet_shows_zero_effective_balance_but_still_lists_row()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var cid = Guid.NewGuid();
            db.Clients.Add(new ClientRow { Id = cid, Plate = "EXP1A00", LojistaId = null });
            db.ClientWallets.Add(new ClientWalletRow
            {
                Id = Guid.NewGuid(),
                ClientId = cid,
                BalanceHours = 50,
                ExpirationDate = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manager/balances-report");
        req.Headers.Authorization = auth;
        req.Headers.Add("X-Parking-Id", park);
        var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var cp = body.GetProperty("clientPlates");
        Assert.Equal(1, cp.GetArrayLength());
        Assert.Equal(0, cp[0].GetProperty("balanceHours").GetInt32());
        Assert.True(cp[0].TryGetProperty("expirationDate", out var exp) && exp.ValueKind != JsonValueKind.Null);

        Assert.Equal(0, body.GetProperty("lojistaBonificadoPlates").GetArrayLength());
    }

    [Fact]
    public async Task Balances_report_plate_query_filters_lojistaBonificadoPlates()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var lojId = Guid.NewGuid();
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = "Loj F",
                HourPrice = 1m,
                AllowGrantBeforeEntry = true,
            });
            db.LojistaWallets.Add(new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = lojId, BalanceHours = 50 });
            var c1 = Guid.NewGuid();
            var c2 = Guid.NewGuid();
            db.Clients.AddRange(
                new ClientRow { Id = c1, Plate = "FIL9A11", LojistaId = lojId },
                new ClientRow { Id = c2, Plate = "ZZZ9Z99", LojistaId = lojId });
            db.ClientWallets.AddRange(
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = c1, BalanceHours = 0, ExpirationDate = null },
                new ClientWalletRow { Id = Guid.NewGuid(), ClientId = c2, BalanceHours = 0, ExpirationDate = null });
            var t0 = DateTimeOffset.UtcNow.AddMinutes(-5);
            db.LojistaGrants.AddRange(
                new LojistaGrantRow
                {
                    Id = Guid.NewGuid(),
                    LojistaId = lojId,
                    ClientId = c1,
                    Plate = "FIL9A11",
                    Hours = 2,
                    GrantMode = "ADVANCE",
                    CreatedAt = t0,
                },
                new LojistaGrantRow
                {
                    Id = Guid.NewGuid(),
                    LojistaId = lojId,
                    ClientId = c2,
                    Plate = "ZZZ9Z99",
                    Hours = 9,
                    GrantMode = "ADVANCE",
                    CreatedAt = t0,
                });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/manager/balances-report?plate=FIL");
        req.Headers.Authorization = auth;
        req.Headers.Add("X-Parking-Id", park);
        var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var bon = body.GetProperty("lojistaBonificadoPlates");
        Assert.Equal(1, bon.GetArrayLength());
        Assert.Equal("FIL9A11", bon[0].GetProperty("plate").GetString());
        Assert.Equal(2, bon[0].GetProperty("balanceHours").GetInt32());
    }
}
