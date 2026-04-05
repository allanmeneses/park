using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Security;
using Parking.Infrastructure.Tenants;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Lojista;

[Collection(nameof(PostgresApiCollection))]
public sealed class LojistaGrantIntegrationTests(PostgresWebAppFixture fx)
{
    private static readonly Guid PkgLoj20h = Guid.Parse("22222222-2222-2222-2222-222222222201");

    private static async Task<(HttpClient http, Guid parkingId, AuthenticationHeaderValue lojAuth)> LojWithBalanceAsync(
        PostgresWebAppFixture fx,
        int buyHoursMinimumViaPackage,
        bool allowGrantBeforeEntry = true)
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var lojId = Guid.NewGuid();
        var lojEmail = $"loj_grant_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = "Loj Grant",
                HourPrice = 1m,
                AllowGrantBeforeEntry = allowGrantBeforeEntry,
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = Guid.NewGuid(),
                Email = lojEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Loj!12345"),
                Role = UserRole.LOJISTA,
                ParkingId = parkingId,
                EntityId = lojId,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await identity.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = lojEmail, password = "Loj!12345" });
        login.EnsureSuccessStatusCode();
        var lojTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var lojAuth = new AuthenticationHeaderValue("Bearer", lojTok);

        while (buyHoursMinimumViaPackage-- > 0)
        {
            using var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/buy")
            {
                Content = JsonContent.Create(new { packageId = PkgLoj20h, settlement = "CREDIT" }),
            };
            buy.Headers.Authorization = lojAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var br = await http.SendAsync(buy);
            br.EnsureSuccessStatusCode();
        }

        return (http, parkingId, lojAuth);
    }

    [Fact]
    public async Task Grant_client_by_plate_transfers_hours_and_lists_history()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "ABC1D23", hours = 3 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
            var gj = await gr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(3, gj.GetProperty("hours").GetInt32());
            Assert.Equal(17, gj.GetProperty("lojista_balance_hours").GetInt32());
            Assert.Equal(3, gj.GetProperty("client_balance_hours").GetInt32());
        }

        using var hist = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/grant-client/history");
        hist.Headers.Authorization = lojAuth;
        var hr = await http.SendAsync(hist);
        hr.EnsureSuccessStatusCode();
        var hj = await hr.Content.ReadFromJsonAsync<JsonElement>();
        var items = hj.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Equal("ABC1D23", items[0].GetProperty("plate").GetString());
        Assert.Equal(3, items[0].GetProperty("hours").GetInt32());
        Assert.Equal("ADVANCE", items[0].GetProperty("grant_mode").GetString());
    }

    [Fact]
    public async Task Grant_to_plate_with_pre_existing_client_row_returns_correct_granted_balance_hours()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var lojId = await db.Lojistas.AsNoTracking().Select(x => x.Id).FirstAsync();
            var clientId = Guid.NewGuid();
            db.Clients.Add(new ClientRow { Id = clientId, Plate = "PRA4658", LojistaId = lojId });
            db.ClientWallets.Add(new ClientWalletRow
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BalanceHours = 0,
                ExpirationDate = null,
            });
            await db.SaveChangesAsync();
        }

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "PRA4658", hours = 1 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
            var gj = await gr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, gj.GetProperty("hours").GetInt32());
            Assert.Equal(1, gj.GetProperty("client_balance_hours").GetInt32());
            Assert.Equal(19, gj.GetProperty("lojista_balance_hours").GetInt32());
        }
    }

    /// <summary>
    /// Modelo antigo debitava carteira lojista com registos <c>wallet_usages</c> sem linha em
    /// <c>lojista_grants</c>. O saldo bonificado não deve ser zerado por esses consumos legados.
    /// </summary>
    [Fact]
    public async Task Grant_first_bonus_ignores_legacy_lojista_wallet_usages_on_closed_ticket()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        const string plate = "LEG9C77";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var entry = DateTimeOffset.UtcNow.AddDays(-40);
            var exit = entry.AddHours(2);
            var ticketId = Guid.NewGuid();
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = plate,
                EntryTime = entry,
                ExitTime = exit,
                Status = Parking.Domain.TicketStatus.CLOSED,
                CreatedAt = entry,
            });
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                Source = "lojista",
                HoursUsed = 5,
            });
            await db.SaveChangesAsync();
        }

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate, hours = 1 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
            var gj = await gr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, gj.GetProperty("client_balance_hours").GetInt32());
        }
    }

    [Fact]
    public async Task Grant_insufficient_balance_returns_LOJISTA_CREDIT_INSUFFICIENT()
    {
        var (http, _, lojAuth) = await LojWithBalanceAsync(fx, 0);

        using var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "XYZ9A87", hours = 1 }),
        };
        grant.Headers.Authorization = lojAuth;
        grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var gr = await http.SendAsync(grant);
        Assert.Equal(HttpStatusCode.Conflict, gr.StatusCode);
        var j = await gr.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOJISTA_CREDIT_INSUFFICIENT", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Grant_by_ticket_id_resolves_plate()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var ticketId = Guid.NewGuid();
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = "DEF5G89",
                EntryTime = now,
                ExitTime = null,
                Status = Parking.Domain.TicketStatus.OPEN,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { ticketId, hours = 2 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
            var gj = await gr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("DEF5G89", gj.GetProperty("plate").GetString());
        }
    }

    [Fact]
    public async Task Grant_when_require_vehicle_present_plate_only_without_open_ticket_returns_GRANT_REQUIRES_ACTIVE_TICKET()
    {
        var (http, _, lojAuth) = await LojWithBalanceAsync(fx, 1, allowGrantBeforeEntry: false);

        using var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "GHT8B12", hours = 1 }),
        };
        grant.Headers.Authorization = lojAuth;
        grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var gr = await http.SendAsync(grant);
        Assert.Equal(HttpStatusCode.Conflict, gr.StatusCode);
        var j = await gr.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GRANT_REQUIRES_ACTIVE_TICKET", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Grant_when_require_vehicle_present_with_open_ticket_by_plate_succeeds()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1, allowGrantBeforeEntry: false);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = Guid.NewGuid(),
                Plate = "MNP7K99",
                EntryTime = now,
                ExitTime = null,
                Status = Parking.Domain.TicketStatus.OPEN,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        using var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "MNP7K99", hours = 1 }),
        };
        grant.Headers.Authorization = lojAuth;
        grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var gr = await http.SendAsync(grant);
        gr.EnsureSuccessStatusCode();
        var gj = await gr.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ON_SITE", gj.GetProperty("grant_mode").GetString());
        // Veículo ainda no pátio: não houve checkout; o saldo bonificado deve refletir o que acabou de ser concedido.
        Assert.Equal(1, gj.GetProperty("client_balance_hours").GetInt32());
    }

    [Fact]
    public async Task Grant_when_require_vehicle_present_closed_ticket_by_id_returns_GRANT_REQUIRES_ACTIVE_TICKET()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1, allowGrantBeforeEntry: false);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var ticketId = Guid.NewGuid();
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = "ZZZ0A00",
                EntryTime = now,
                ExitTime = now,
                Status = Parking.Domain.TicketStatus.CLOSED,
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        using var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { ticketId, hours = 1 }),
        };
        grant.Headers.Authorization = lojAuth;
        grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var gr = await http.SendAsync(grant);
        Assert.Equal(HttpStatusCode.Conflict, gr.StatusCode);
        var j = await gr.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GRANT_REQUIRES_ACTIVE_TICKET", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Grant_settings_get_put_roundtrip()
    {
        var (http, _, lojAuth) = await LojWithBalanceAsync(fx, 0);

        using (var get = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/grant-settings"))
        {
            get.Headers.Authorization = lojAuth;
            var r = await http.SendAsync(get);
            r.EnsureSuccessStatusCode();
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(j.GetProperty("allow_grant_before_entry").GetBoolean());
        }

        using (var put = new HttpRequestMessage(HttpMethod.Put, "/api/v1/lojista/grant-settings"))
        {
            put.Headers.Authorization = lojAuth;
            put.Content = JsonContent.Create(new Dictionary<string, bool> { ["allow_grant_before_entry"] = false });
            var r = await http.SendAsync(put);
            r.EnsureSuccessStatusCode();
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(j.GetProperty("allow_grant_before_entry").GetBoolean());
        }

        using (var get2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/grant-settings"))
        {
            get2.Headers.Authorization = lojAuth;
            var r = await http.SendAsync(get2);
            r.EnsureSuccessStatusCode();
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(j.GetProperty("allow_grant_before_entry").GetBoolean());
        }
    }

    [Fact]
    public async Task Grant_history_filter_by_plate()
    {
        var (http, _, lojAuth) = await LojWithBalanceAsync(fx, 2);

        async Task grantPlate(string plate, int h)
        {
            using var g = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
            {
                Content = JsonContent.Create(new { plate, hours = h }),
            };
            g.Headers.Authorization = lojAuth;
            g.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            (await http.SendAsync(g)).EnsureSuccessStatusCode();
        }

        await grantPlate("AAA1111", 1);
        await grantPlate("BBB2222", 1);

        using var h = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/grant-client/history?plate=AAA1111");
        h.Headers.Authorization = lojAuth;
        var r = await http.SendAsync(h);
        r.EnsureSuccessStatusCode();
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        var arr = j.GetProperty("items");
        Assert.True(arr.GetArrayLength() >= 1);
        foreach (var el in arr.EnumerateArray())
        {
            Assert.Equal("AAA1111", el.GetProperty("plate").GetString());
            Assert.True(el.TryGetProperty("grant_mode", out _));
        }
    }

    [Fact]
    public async Task Checkout_consumes_client_granted_balance_before_direct_lojista_wallet()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "GRT1A23", hours = 3 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
        }

        var superLogin = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        superLogin.EnsureSuccessStatusCode();
        var superToken = (await superLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var adminAuth = new AuthenticationHeaderValue("Bearer", superToken);

        Guid ticketId;
        using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            t1.Headers.Authorization = adminAuth;
            t1.Headers.Add("X-Parking-Id", parkingId.ToString());
            t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t1.Content = JsonContent.Create(new { plate = "GRT1A23", entry_time = DateTimeOffset.UtcNow.AddMinutes(-40) });
            var tr = await http.SendAsync(t1);
            tr.EnsureSuccessStatusCode();
            ticketId = (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        using (var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout"))
        {
            t2.Headers.Authorization = adminAuth;
            t2.Headers.Add("X-Parking-Id", parkingId.ToString());
            t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow });
            var cr = await http.SendAsync(t2);
            cr.EnsureSuccessStatusCode();
            var j = await cr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, j.GetProperty("hours_cliente").GetInt32());
            Assert.Equal(1, j.GetProperty("hours_lojista").GetInt32());
        }

        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        await using var scope = fx.Factory.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var db = factory.CreateReadWrite(cs);
        var client = await db.Clients.AsNoTracking().FirstAsync(x => x.Plate == "GRT1A23");
        var cw = await db.ClientWallets.AsNoTracking().FirstAsync(x => x.ClientId == client.Id);
        Assert.Equal(0, cw.BalanceHours);
    }

    [Fact]
    public async Task Checkout_does_not_use_lojista_wallet_automatically_after_client_grant_is_consumed()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "INF1N10", hours = 1 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
        }

        var superLogin = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        superLogin.EnsureSuccessStatusCode();
        var superToken = (await superLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var adminAuth = new AuthenticationHeaderValue("Bearer", superToken);

        async Task<JsonElement> checkoutOnce(string plate)
        {
            Guid ticketId;
            using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
            {
                t1.Headers.Authorization = adminAuth;
                t1.Headers.Add("X-Parking-Id", parkingId.ToString());
                t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
                t1.Content = JsonContent.Create(new { plate, entry_time = DateTimeOffset.UtcNow.AddMinutes(-40) });
                var tr = await http.SendAsync(t1);
                tr.EnsureSuccessStatusCode();
                ticketId = (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            }

            using var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout");
            t2.Headers.Authorization = adminAuth;
            t2.Headers.Add("X-Parking-Id", parkingId.ToString());
            t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow });
            var cr = await http.SendAsync(t2);
            cr.EnsureSuccessStatusCode();
            return (await cr.Content.ReadFromJsonAsync<JsonElement>());
        }

        var first = await checkoutOnce("INF1N10");
        Assert.Equal(0, first.GetProperty("hours_cliente").GetInt32());
        Assert.Equal(1, first.GetProperty("hours_lojista").GetInt32());
        Assert.Equal("0.00", first.GetProperty("amount").GetString());

        var second = await checkoutOnce("INF1N10");
        Assert.Equal(0, second.GetProperty("hours_cliente").GetInt32());
        Assert.Equal(0, second.GetProperty("hours_lojista").GetInt32());
        Assert.NotEqual("0.00", second.GetProperty("amount").GetString());
    }

    [Fact]
    public async Task Checkout_awaiting_payment_can_recalculate_after_new_grant_for_same_ticket()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);
        var superLogin = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        superLogin.EnsureSuccessStatusCode();
        var superToken = (await superLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var adminAuth = new AuthenticationHeaderValue("Bearer", superToken);

        Guid ticketId;
        using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            t1.Headers.Authorization = adminAuth;
            t1.Headers.Add("X-Parking-Id", parkingId.ToString());
            t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t1.Content = JsonContent.Create(new { plate = "RCL1A23", entry_time = DateTimeOffset.UtcNow.AddMinutes(-40) });
            var tr = await http.SendAsync(t1);
            tr.EnsureSuccessStatusCode();
            ticketId = (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        using (var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout"))
        {
            t2.Headers.Authorization = adminAuth;
            t2.Headers.Add("X-Parking-Id", parkingId.ToString());
            t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow });
            var cr = await http.SendAsync(t2);
            cr.EnsureSuccessStatusCode();
            var j = await cr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.NotEqual("0.00", j.GetProperty("amount").GetString());
            Assert.Equal(0, j.GetProperty("hours_cliente").GetInt32());
        }

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { ticketId, hours = 1 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var gr = await http.SendAsync(grant);
            gr.EnsureSuccessStatusCode();
        }

        using (var t3 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout"))
        {
            t3.Headers.Authorization = adminAuth;
            t3.Headers.Add("X-Parking-Id", parkingId.ToString());
            t3.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t3.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow });
            var cr = await http.SendAsync(t3);
            cr.EnsureSuccessStatusCode();
            var j = await cr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("0.00", j.GetProperty("amount").GetString());
            Assert.Equal(0, j.GetProperty("hours_cliente").GetInt32());
            Assert.Equal(1, j.GetProperty("hours_lojista").GetInt32());
        }
    }

    [Fact]
    public async Task Checkout_consumes_lojista_grant_before_client_wallet_when_both_exist()
    {
        var (http, parkingId, lojAuth) = await LojWithBalanceAsync(fx, 1);
        var superLogin = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        superLogin.EnsureSuccessStatusCode();
        var superToken = (await superLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var adminAuth = new AuthenticationHeaderValue("Bearer", superToken);

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate = "ORD1A23", hours = 1 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            (await http.SendAsync(grant)).EnsureSuccessStatusCode();
        }

        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var client = await db.Clients.FirstAsync(x => x.Plate == "ORD1A23");
            var cw = await db.ClientWallets.FirstOrDefaultAsync(x => x.ClientId == client.Id);
            if (cw == null)
            {
                cw = new ClientWalletRow { Id = Guid.NewGuid(), ClientId = client.Id, BalanceHours = 0, ExpirationDate = null };
                db.ClientWallets.Add(cw);
            }
            cw.BalanceHours = 2;
            await db.SaveChangesAsync();
        }

        Guid ticketId;
        using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            t1.Headers.Authorization = adminAuth;
            t1.Headers.Add("X-Parking-Id", parkingId.ToString());
            t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t1.Content = JsonContent.Create(new { plate = "ORD1A23" });
            var tr = await http.SendAsync(t1);
            tr.EnsureSuccessStatusCode();
            ticketId = (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var tk = await db.Tickets.FirstAsync(x => x.Id == ticketId);
            tk.EntryTime = DateTimeOffset.UtcNow.AddMinutes(-121);
            await db.SaveChangesAsync();
        }

        using (var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout"))
        {
            t2.Headers.Authorization = adminAuth;
            t2.Headers.Add("X-Parking-Id", parkingId.ToString());
            t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow });
            var cr = await http.SendAsync(t2);
            cr.EnsureSuccessStatusCode();
            var j = await cr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, j.GetProperty("hours_lojista").GetInt32());
            Assert.Equal(2, j.GetProperty("hours_cliente").GetInt32());
            Assert.Equal("0.00", j.GetProperty("amount").GetString());
        }
    }

    /// <summary>
    /// Saldo bonificado vem de <c>lojista_grants</c> por placa; o checkout deve consumir bonificação antes da
    /// carteira comprada mesmo quando <c>clients.lojista_id</c> está null (ex.: dados legados ou cadastro sem vínculo).
    /// </summary>
    [Fact]
    public async Task Checkout_uses_plate_bonificado_before_client_wallet_when_client_lojista_id_is_null()
    {
        var (http, parkingId, _) = await LojWithBalanceAsync(fx, 1);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        const string plate = "NUL8A88";

        Guid lojId;
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            lojId = await db.Lojistas.AsNoTracking().Select(x => x.Id).FirstAsync();
            var clientId = Guid.NewGuid();
            db.Clients.Add(new ClientRow { Id = clientId, Plate = plate, LojistaId = null });
            db.ClientWallets.Add(new ClientWalletRow
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                BalanceHours = 5,
                ExpirationDate = null,
            });
            db.LojistaGrants.Add(new LojistaGrantRow
            {
                Id = Guid.NewGuid(),
                LojistaId = lojId,
                ClientId = clientId,
                Plate = plate,
                Hours = 2,
                GrantMode = "ADVANCE",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync();
        }

        var superLogin = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        superLogin.EnsureSuccessStatusCode();
        var superToken = (await superLogin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var adminAuth = new AuthenticationHeaderValue("Bearer", superToken);

        Guid ticketId;
        using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            t1.Headers.Authorization = adminAuth;
            t1.Headers.Add("X-Parking-Id", parkingId.ToString());
            t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t1.Content = JsonContent.Create(new { plate, entry_time = DateTimeOffset.UtcNow.AddMinutes(-40) });
            var tr = await http.SendAsync(t1);
            tr.EnsureSuccessStatusCode();
            ticketId = (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        using (var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout"))
        {
            t2.Headers.Authorization = adminAuth;
            t2.Headers.Add("X-Parking-Id", parkingId.ToString());
            t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow });
            var cr = await http.SendAsync(t2);
            cr.EnsureSuccessStatusCode();
            var j = await cr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, j.GetProperty("hours_lojista").GetInt32());
            Assert.Equal(0, j.GetProperty("hours_cliente").GetInt32());
        }

        await using (var scope2 = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope2.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var client = await db.Clients.AsNoTracking().FirstAsync(x => x.Plate == plate);
            var cw = await db.ClientWallets.AsNoTracking().FirstAsync(x => x.ClientId == client.Id);
            Assert.Equal(5, cw.BalanceHours);
        }
    }
}
