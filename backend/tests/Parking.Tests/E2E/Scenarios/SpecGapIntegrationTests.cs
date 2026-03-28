using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Api.Hosting;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Security;
using Parking.Infrastructure.Tenants;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Scenarios;

/// <summary>SPEC §24 <c>Scenarios</c> — Checkout, Packages, Payments (PIX expiry), Dashboard.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class SpecGapIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Checkout_com_X_Device_Time_com_skew_retorna_CLOCK_SKEW()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminToken) = await E2ETenantProvision.NewTenantWithAdminAsync(http);

        using var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets");
        t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        t1.Headers.Add("X-Parking-Id", parkingId.ToString());
        t1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        t1.Content = JsonContent.Create(new { plate = "ABC9D99" });
        var ticketRes = await http.SendAsync(t1);
        ticketRes.EnsureSuccessStatusCode();
        var ticketId = (await ticketRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout");
        t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        t2.Headers.Add("X-Parking-Id", parkingId.ToString());
        t2.Headers.Add("X-Device-Time", DateTimeOffset.UtcNow.AddHours(1).ToString("O"));
        t2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow.AddMinutes(30) });
        var checkoutRes = await http.SendAsync(t2);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, checkoutRes.StatusCode);
        var body = await checkoutRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CLOCK_SKEW", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Client_history_retorna_PURCHASE_e_USAGE()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var clientId = Guid.NewGuid();
        var cliEmail = $"cli_hist_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Clients.Add(new ClientRow { Id = clientId, Plate = "CLIHIST1", LojistaId = null });
            var pkgId = Guid.NewGuid();
            db.RechargePackages.Add(new RechargePackageRow
            {
                Id = pkgId,
                Scope = "CLIENT",
                Hours = 10,
                Price = 50m,
                Active = true
            });
            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                LojistaId = null,
                DeltaHours = 5,
                Amount = 50m,
                PackageId = pkgId,
                Settlement = "CREDIT",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3)
            });

            var ticketId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = "CLIHIST1",
                EntryTime = now.AddHours(-5),
                ExitTime = now.AddHours(-1),
                Status = TicketStatus.CLOSED,
                CreatedAt = now.AddHours(-5)
            });
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                Source = "client",
                HoursUsed = 2
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = Guid.NewGuid(),
                Email = cliEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Cli!12345"),
                Role = UserRole.CLIENT,
                ParkingId = parkingId,
                EntityId = clientId,
                Active = true,
                OperatorSuspended = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = cliEmail, password = "Cli!12345" });
        login.EnsureSuccessStatusCode();
        var cliTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using var hr = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client/history?limit=50");
        hr.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cliTok);
        var histRes = await http.SendAsync(hr);
        histRes.EnsureSuccessStatusCode();
        var doc = await histRes.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
    }

    [Fact]
    public async Task PixExpiryRunner_marca_pagamento_EXPIRED()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        Guid paymentId;
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var ticketId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = "PIXEXP1",
                EntryTime = now.AddHours(-2),
                ExitTime = now,
                Status = TicketStatus.AWAITING_PAYMENT,
                CreatedAt = now.AddHours(-2)
            });
            paymentId = Guid.NewGuid();
            db.Payments.Add(new PaymentRow
            {
                Id = paymentId,
                TicketId = ticketId,
                PackageOrderId = null,
                Method = null,
                Status = PaymentStatus.PENDING,
                Amount = 15m,
                TransactionId = null,
                IdempotencyKey = Guid.NewGuid().ToString(),
                CreatedAt = now,
                PaidAt = null,
                FailedReason = null
            });
            db.PixTransactions.Add(new PixTransactionRow
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId,
                ProviderStatus = "CREATED",
                QrCode = "PIXSTUB|" + new string('0', 24),
                ExpiresAt = now.AddMinutes(-10),
                TransactionId = null,
                Active = true
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var runner = scope.ServiceProvider.GetRequiredService<PixExpiryRunner>();
            await runner.RunForAllTenantsAsync(identity, CancellationToken.None);
        }

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var p = await db.Payments.AsNoTracking().FirstAsync(x => x.Id == paymentId);
            Assert.Equal(PaymentStatus.EXPIRED, p.Status);
            Assert.Equal("PIX_EXPIRED", p.FailedReason);
        }
    }

    [Fact]
    public async Task Dashboard_retorna_uso_convenio_quando_houver_fechamentos_hoje()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminToken) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var lojId = Guid.NewGuid();
            db.Lojistas.Add(new LojistaRow { Id = lojId, Name = "L1", HourPrice = 1m });
            db.LojistaWallets.Add(new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = lojId, BalanceHours = 100 });
            var clientId = Guid.NewGuid();
            db.Clients.Add(new ClientRow { Id = clientId, Plate = "CONV1", LojistaId = lojId });

            var ticketId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            db.Tickets.Add(new TicketRow
            {
                Id = ticketId,
                Plate = "CONV1",
                EntryTime = now.AddHours(-2),
                ExitTime = now,
                Status = TicketStatus.CLOSED,
                CreatedAt = now.AddHours(-2)
            });
            db.WalletUsages.Add(new WalletUsageRow
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                Source = "lojista",
                HoursUsed = 1
            });
            var payId = Guid.NewGuid();
            db.Payments.Add(new PaymentRow
            {
                Id = payId,
                TicketId = ticketId,
                PackageOrderId = null,
                Method = PaymentMethod.PIX,
                Status = PaymentStatus.PAID,
                Amount = 10m,
                TransactionId = null,
                IdempotencyKey = Guid.NewGuid().ToString(),
                CreatedAt = now,
                PaidAt = now,
                FailedReason = null
            });
            await db.SaveChangesAsync();
        }

        using var dr = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard");
        dr.Headers.Add("X-Parking-Id", parkingId.ToString());
        dr.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var dash = await http.SendAsync(dr);
        dash.EnsureSuccessStatusCode();
        var j = await dash.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(j.TryGetProperty("uso_convenio", out var uc));
        Assert.Equal(1.0, uc.GetDouble());
    }

    [Fact]
    public async Task E2e_seed_client_with_history_cria_login_e_historico_com_2_itens()
    {
        var http = fx.Factory.CreateClient();
        var (_, adminToken) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var email = $"e2e_seed_cli_{Guid.NewGuid():N}@test.local";
        using var seed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/e2e/client-with-history")
        {
            Content = JsonContent.Create(new { email, password = "Cli!12345" }),
        };
        seed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var seedRes = await http.SendAsync(seed);
        Assert.True(seedRes.IsSuccessStatusCode, await seedRes.Content.ReadAsStringAsync());

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Cli!12345" });
        login.EnsureSuccessStatusCode();
        var cliTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using var hr = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client/history?limit=50");
        hr.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cliTok);
        var histRes = await http.SendAsync(hr);
        histRes.EnsureSuccessStatusCode();
        var doc = await histRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, doc.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task E2e_seed_lojista_with_history_cria_login_e_historico_com_2_itens()
    {
        var http = fx.Factory.CreateClient();
        var (_, adminToken) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var email = $"e2e_seed_loj_{Guid.NewGuid():N}@test.local";
        using var seed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/e2e/lojista-with-history")
        {
            Content = JsonContent.Create(new { email, password = "Loj!12345" }),
        };
        seed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var seedRes = await http.SendAsync(seed);
        Assert.True(seedRes.IsSuccessStatusCode, await seedRes.Content.ReadAsStringAsync());

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Loj!12345" });
        login.EnsureSuccessStatusCode();
        var tok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using var hr = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/history?limit=50");
        hr.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        var histRes = await http.SendAsync(hr);
        histRes.EnsureSuccessStatusCode();
        var doc = await histRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, doc.GetProperty("items").GetArrayLength());
    }
}
