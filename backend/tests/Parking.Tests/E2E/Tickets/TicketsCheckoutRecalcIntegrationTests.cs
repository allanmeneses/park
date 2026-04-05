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

namespace Parking.Tests.E2E.Tickets;

/// <summary>
/// Recálculo de checkout em AWAITING_PAYMENT: sem <c>exit_time</c> no corpo, a saída deve ser o instante atual do servidor
/// (nova tentativa de pagamento após o cliente permanecer no pátio).
/// </summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class TicketsCheckoutRecalcIntegrationTests(PostgresWebAppFixture fx)
{
    private static readonly Guid PkgLoj20h = Guid.Parse("22222222-2222-2222-2222-222222222201");

    [Fact]
    public async Task Checkout_recalc_sem_exit_time_usa_agora_UTC_e_atualiza_valor()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        Guid ticketId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets")
        {
            Content = JsonContent.Create(new { plate = "RCL9Z88" }),
        })
        {
            create.Headers.Authorization = auth;
            create.Headers.Add("X-Parking-Id", park);
            create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var cr = await http.SendAsync(create);
            cr.EnsureSuccessStatusCode();
            ticketId = (await cr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        // Entrada ~2h30 antes do recálculo: após novo checkout (saída ≈ agora), ceil ≈ 3 h — margem para não
        // cruzar 3h exatos (o que viraria 4 h faturáveis com qualquer ε>0).
        var anchor = DateTimeOffset.UtcNow;
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var tk = await db.Tickets.FirstAsync(x => x.Id == ticketId);
            tk.EntryTime = anchor.AddHours(-2).AddMinutes(-30);
            await db.SaveChangesAsync();
        }

        var exitPrimeira = anchor.AddHours(-2).AddMinutes(-20);
        JsonElement pay1;
        using (var ch1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout")
        {
            Content = JsonContent.Create(new { exit_time = exitPrimeira }),
        })
        {
            ch1.Headers.Authorization = auth;
            ch1.Headers.Add("X-Parking-Id", park);
            ch1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var r1 = await http.SendAsync(ch1);
            r1.EnsureSuccessStatusCode();
            pay1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal("5.00", pay1.GetProperty("amount").GetString());
        Assert.Equal(1, pay1.GetProperty("hours_total").GetInt32());

        await Task.Delay(1500);

        JsonElement pay2;
        using (var ch2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout")
        {
            Content = JsonContent.Create(new { }),
        })
        {
            ch2.Headers.Authorization = auth;
            ch2.Headers.Add("X-Parking-Id", park);
            ch2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var r2 = await http.SendAsync(ch2);
            r2.EnsureSuccessStatusCode();
            pay2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal(pay1.GetProperty("payment_id").GetGuid(), pay2.GetProperty("payment_id").GetGuid());
        Assert.Equal(3, pay2.GetProperty("hours_total").GetInt32());
        Assert.Equal("15.00", pay2.GetProperty("amount").GetString());

        await using (var scope2 = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope2.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var tk = await db.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);
            Assert.Equal(TicketStatus.AWAITING_PAYMENT, tk.Status);
            Assert.True(tk.ExitTime > exitPrimeira);
        }
    }

    /// <summary>
    /// Recálculo não pode “esquecer” horas bonificadas: sem flush dos DELETE em wallet_usages, o saldo
    /// bonificado via SumGrantScopedLojistaUsedHoursAsync ainda via o uso deste ticket e zerava horas_lojista.
    /// </summary>
    [Fact]
    public async Task Checkout_recalc_mantem_horas_lojista_bonificadas()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        const string plate = "AAA1111";
        var lojId = Guid.NewGuid();
        var lojEmail = $"loj_recalc_grant_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = "Loj Recalc",
                HourPrice = 1m,
                AllowGrantBeforeEntry = true,
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

        var loginLoj = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = lojEmail, password = "Loj!12345" });
        loginLoj.EnsureSuccessStatusCode();
        var lojTok = (await loginLoj.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var lojAuth = new AuthenticationHeaderValue("Bearer", lojTok);

        using (var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/buy")
        {
            Content = JsonContent.Create(new { packageId = PkgLoj20h, settlement = "CREDIT" }),
        })
        {
            buy.Headers.Authorization = lojAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            (await http.SendAsync(buy)).EnsureSuccessStatusCode();
        }

        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate, hours = 1 }),
        })
        {
            grant.Headers.Authorization = lojAuth;
            grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            (await http.SendAsync(grant)).EnsureSuccessStatusCode();
        }

        Guid ticketId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets")
        {
            Content = JsonContent.Create(new { plate }),
        })
        {
            create.Headers.Authorization = auth;
            create.Headers.Add("X-Parking-Id", park);
            create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var cr = await http.SendAsync(create);
            cr.EnsureSuccessStatusCode();
            ticketId = (await cr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var anchor = DateTimeOffset.UtcNow;
        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            var tk = await db.Tickets.FirstAsync(x => x.Id == ticketId);
            tk.EntryTime = anchor.AddHours(-5);
            await db.SaveChangesAsync();
        }

        var exitPrimeira = anchor.AddSeconds(-45);
        JsonElement pay1;
        using (var ch1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout")
        {
            Content = JsonContent.Create(new { exit_time = exitPrimeira }),
        })
        {
            ch1.Headers.Authorization = auth;
            ch1.Headers.Add("X-Parking-Id", park);
            ch1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var r1 = await http.SendAsync(ch1);
            r1.EnsureSuccessStatusCode();
            pay1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal(5, pay1.GetProperty("hours_total").GetInt32());
        Assert.Equal(1, pay1.GetProperty("hours_lojista").GetInt32());
        Assert.Equal("20.00", pay1.GetProperty("amount").GetString());

        await Task.Delay(1500);

        JsonElement pay2;
        using (var ch2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout")
        {
            Content = JsonContent.Create(new { }),
        })
        {
            ch2.Headers.Authorization = auth;
            ch2.Headers.Add("X-Parking-Id", park);
            ch2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var r2 = await http.SendAsync(ch2);
            r2.EnsureSuccessStatusCode();
            pay2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
        }

        Assert.Equal(pay1.GetProperty("payment_id").GetGuid(), pay2.GetProperty("payment_id").GetGuid());
        Assert.Equal(1, pay2.GetProperty("hours_lojista").GetInt32());
        var ht = pay2.GetProperty("hours_total").GetInt32();
        var hp = pay2.GetProperty("hours_paid").GetInt32();
        Assert.Equal(ht - 1, hp);
        Assert.Equal(MoneyForHours(ht - 1), pay2.GetProperty("amount").GetString());
    }

    private static string MoneyForHours(int payableHours) => (payableHours * 5m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
