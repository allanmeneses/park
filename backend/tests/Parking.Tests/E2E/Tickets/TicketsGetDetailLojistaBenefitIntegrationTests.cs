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

[Collection(nameof(PostgresApiCollection))]
public sealed class TicketsGetDetailLojistaBenefitIntegrationTests(PostgresWebAppFixture fx)
{
    private static readonly Guid PkgLoj20h = Guid.Parse("22222222-2222-2222-2222-222222222201");

    [Fact]
    public async Task Get_ticket_inclui_lojistaBenefits_array_com_nome_e_horas_quando_existe_convenio()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        const string plate = "AAA1111";
        const string lojDisplayName = "Loj Resumo Convênio";
        var lojId = Guid.NewGuid();
        var lojEmail = $"loj_get_ticket_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = lojDisplayName,
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

        const int grantedHours = 3;
        using (var grant = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/grant-client")
        {
            Content = JsonContent.Create(new { plate, hours = grantedHours }),
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

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tickets/{ticketId}");
        get.Headers.Authorization = auth;
        get.Headers.Add("X-Parking-Id", park);
        var gr = await http.SendAsync(get);
        gr.EnsureSuccessStatusCode();
        var body = await gr.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("lojistaBenefits", out var arr));
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());
        var ben = arr[0];
        Assert.Equal(lojId.ToString(), ben.GetProperty("lojistaId").GetString());
        Assert.Equal(lojDisplayName, ben.GetProperty("lojistaName").GetString());
        Assert.Equal(grantedHours, ben.GetProperty("hoursAvailable").GetInt32());
        Assert.Equal(grantedHours, ben.GetProperty("hoursGrantedTotal").GetInt32());
    }

    [Fact]
    public async Task Get_ticket_lojistaBenefits_vazio_sem_convenio_na_placa()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

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

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tickets/{ticketId}");
        get.Headers.Authorization = auth;
        get.Headers.Add("X-Parking-Id", park);
        var gr = await http.SendAsync(get);
        gr.EnsureSuccessStatusCode();
        var body = await gr.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("lojistaBenefits", out var arr));
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(0, arr.GetArrayLength());
    }

    [Fact]
    public async Task Get_ticket_ignora_bonificacao_de_dia_anterior_quando_regra_do_estacionamento_e_somente_no_mesmo_dia()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);

        const string plate = "EXP4D23";
        var lojId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Settings.Add(new SettingsRow
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000000"),
                PricePerHour = 5m,
                Capacity = 50,
            });
            db.Lojistas.Add(new LojistaRow
            {
                Id = lojId,
                Name = "Loj Expira",
                HourPrice = 1m,
                AllowGrantBeforeEntry = true,
            });
            db.Clients.Add(new ClientRow { Id = clientId, Plate = plate, LojistaId = lojId });
            db.ClientWallets.Add(new ClientWalletRow { Id = Guid.NewGuid(), ClientId = clientId, BalanceHours = 0, ExpirationDate = null });
            db.LojistaGrants.Add(new LojistaGrantRow
            {
                Id = Guid.NewGuid(),
                LojistaId = lojId,
                ClientId = clientId,
                Plate = plate,
                Hours = 4,
                GrantMode = "ADVANCE",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        using (var saveSettings = new HttpRequestMessage(HttpMethod.Post, "/api/v1/settings"))
        {
            saveSettings.Headers.Authorization = auth;
            saveSettings.Headers.Add("X-Parking-Id", park);
            saveSettings.Content = JsonContent.Create(new { pricePerHour = 5m, capacity = 50, lojistaGrantSameDayOnly = true });
            (await http.SendAsync(saveSettings)).EnsureSuccessStatusCode();
        }

        Guid ticketId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            create.Headers.Authorization = auth;
            create.Headers.Add("X-Parking-Id", park);
            create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            create.Content = JsonContent.Create(new { plate });
            var cr = await http.SendAsync(create);
            cr.EnsureSuccessStatusCode();
            ticketId = (await cr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tickets/{ticketId}");
        get.Headers.Authorization = auth;
        get.Headers.Add("X-Parking-Id", park);
        var gr = await http.SendAsync(get);
        gr.EnsureSuccessStatusCode();
        var body = await gr.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("lojistaBenefits", out var arr));
        Assert.Equal(0, arr.GetArrayLength());
    }
}
