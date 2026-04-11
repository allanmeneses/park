using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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

namespace Parking.Tests.E2E.Surface;

/// <summary>SPEC §17/§18 — cobre rotas antes marcadas como *parcial*: payments card/cash/404, client/lojista wallet+buy, admin unsuspend feliz.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class ExtendedApiRoutesIntegrationTests(PostgresWebAppFixture fx)
{
    private static readonly Guid PkgClient10h = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid PkgLoj20h = Guid.Parse("22222222-2222-2222-2222-222222222201");

    [Fact]
    public async Task Post_payments_card_com_valor_correto_fecha_ticket()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        var ticketId = await PostTicketAsync(http, park, auth, "CRD1X99");
        var (paymentId, amountStr) = await PostCheckoutAsync(http, park, auth, ticketId);

        using var card = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/card")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    paymentId,
                    amount = ParseApiMoney(amountStr),
                }),
                Encoding.UTF8,
                "application/json"),
        };
        card.Headers.Authorization = auth;
        card.Headers.Add("X-Parking-Id", park);
        var cardRes = await http.SendAsync(card);
        cardRes.EnsureSuccessStatusCode();

        Assert.Equal("CLOSED", await GetTicketStatusAsync(http, park, auth, ticketId));
    }

    [Fact]
    public async Task Post_payments_cash_com_caixa_aberto_fecha_ticket()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        using (var open = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/open"))
        {
            open.Headers.Authorization = auth;
            open.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(open);
            r.EnsureSuccessStatusCode();
        }

        var ticketId = await PostTicketAsync(http, park, auth, "CSH1Y88");
        var (paymentId, _) = await PostCheckoutAsync(http, park, auth, ticketId);

        using var cash = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/cash")
        {
            Content = JsonContent.Create(new { paymentId }),
        };
        cash.Headers.Authorization = auth;
        cash.Headers.Add("X-Parking-Id", park);
        var cashRes = await http.SendAsync(cash);
        cashRes.EnsureSuccessStatusCode();

        Assert.Equal("CLOSED", await GetTicketStatusAsync(http, park, auth, ticketId));
    }

    [Fact]
    public async Task Get_payment_id_inexistente_retorna_NOT_FOUND()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var ghost = Guid.Parse("deadbeef-dead-dead-dead-deaddeadbeef");

        using var g = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/payments/{ghost}");
        g.Headers.Authorization = auth;
        g.Headers.Add("X-Parking-Id", park);
        var res = await http.SendAsync(g);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NOT_FOUND", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_payments_card_valor_errado_retorna_AMOUNT_MISMATCH()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        var ticketId = await PostTicketAsync(http, park, auth, "MIS1Z77");
        var (paymentId, amountStr) = await PostCheckoutAsync(http, park, auth, ticketId);
        var amount = ParseApiMoney(amountStr);
        var wrong = amount + 99m;

        using var card = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/card")
        {
            Content = JsonContent.Create(new { paymentId, amount = wrong }),
        };
        card.Headers.Authorization = auth;
        card.Headers.Add("X-Parking-Id", park);
        var cardRes = await http.SendAsync(card);
        Assert.Equal(HttpStatusCode.Conflict, cardRes.StatusCode);
        var j = await cardRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AMOUNT_MISMATCH", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Client_GET_wallet_POST_buy_credito_GET_wallet_saldo()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var clientId = Guid.NewGuid();
        var cliEmail = $"cli_buy_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Clients.Add(new ClientRow { Id = clientId, Plate = "CLIBUY1", LojistaId = null });
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
        var cliAuth = new AuthenticationHeaderValue("Bearer", cliTok);

        using (var w0 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client/wallet"))
        {
            w0.Headers.Authorization = cliAuth;
            var r0 = await http.SendAsync(w0);
            r0.EnsureSuccessStatusCode();
            var j0 = await r0.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, j0.GetProperty("balance_hours").GetInt32());
        }

        using (var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client/buy")
        {
            Content = JsonContent.Create(new { packageId = PkgClient10h, settlement = "CREDIT" }),
        })
        {
            buy.Headers.Authorization = cliAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var br = await http.SendAsync(buy);
            br.EnsureSuccessStatusCode();
            var bj = await br.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("PAID", bj.GetProperty("status").GetString());
            Assert.Equal(10, bj.GetProperty("balance_hours").GetInt32());
        }

        using (var w1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client/wallet"))
        {
            w1.Headers.Authorization = cliAuth;
            var r1 = await http.SendAsync(w1);
            r1.EnsureSuccessStatusCode();
            var j1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(10, j1.GetProperty("balance_hours").GetInt32());
        }
    }

    [Fact]
    public async Task Client_buy_pix_GET_payment_como_cliente_retorna_200()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var clientId = Guid.NewGuid();
        var cliEmail = $"cli_pix_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Clients.Add(new ClientRow { Id = clientId, Plate = "CLIPIX1", LojistaId = null });
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
        var cliAuth = new AuthenticationHeaderValue("Bearer", cliTok);

        Guid paymentId;
        using (var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/client/buy")
        {
            Content = JsonContent.Create(new { packageId = PkgClient10h, settlement = "PIX" }),
        })
        {
            buy.Headers.Authorization = cliAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var br = await http.SendAsync(buy);
            br.EnsureSuccessStatusCode();
            var bj = await br.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("AWAITING_PAYMENT", bj.GetProperty("status").GetString());
            paymentId = bj.GetProperty("payment_id").GetGuid();
        }

        using (var pix = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/pix")
        {
            Content = JsonContent.Create(new { payment_id = paymentId }),
        })
        {
            pix.Headers.Authorization = cliAuth;
            var pr = await http.SendAsync(pix);
            pr.EnsureSuccessStatusCode();
            var pj = await pr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(paymentId, pj.GetProperty("payment_id").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(pj.GetProperty("qr_code").GetString()));
        }

        using (var gp = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/payments/{paymentId}"))
        {
            gp.Headers.Authorization = cliAuth;
            var pr = await http.SendAsync(gp);
            pr.EnsureSuccessStatusCode();
            var pj = await pr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(paymentId.ToString(), pj.GetProperty("id").GetString());
            Assert.Equal("PENDING", pj.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Lojista_buy_pix_POST_payments_pix_webhook_credita_carteira()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var lojId = Guid.NewGuid();
        var lojEmail = $"loj_pix_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Lojistas.Add(new LojistaRow { Id = lojId, Name = "Loj PIX", HourPrice = 10m });
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
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = lojEmail, password = "Loj!12345" });
        login.EnsureSuccessStatusCode();
        var lojTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var lojAuth = new AuthenticationHeaderValue("Bearer", lojTok);

        Guid paymentId;
        using (var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/buy")
        {
            Content = JsonContent.Create(new { packageId = PkgLoj20h, settlement = "PIX" }),
        })
        {
            buy.Headers.Authorization = lojAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var br = await http.SendAsync(buy);
            br.EnsureSuccessStatusCode();
            var bj = await br.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("AWAITING_PAYMENT", bj.GetProperty("status").GetString());
            paymentId = bj.GetProperty("payment_id").GetGuid();
        }

        using (var pix = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/pix")
        {
            Content = JsonContent.Create(new { payment_id = paymentId }),
        })
        {
            pix.Headers.Authorization = lojAuth;
            var pr = await http.SendAsync(pix);
            pr.EnsureSuccessStatusCode();
            var pj = await pr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(paymentId, pj.GetProperty("payment_id").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(pj.GetProperty("qr_code").GetString()));
        }

        var raw = JsonSerializer.Serialize(new
        {
            transaction_id = Guid.NewGuid().ToString("N"),
            payment_id = paymentId,
            status = "PAID"
        });
        using (var mac = new HMACSHA256(Encoding.UTF8.GetBytes(new string('b', 32))))
        {
            var sig = Convert.ToHexStringLower(mac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
            using var wh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
            {
                Content = new StringContent(raw, Encoding.UTF8, "application/json")
            };
            wh.Headers.Add("X-Parking-Id", parkingId.ToString());
            wh.Headers.Add("X-Signature", sig);
            var wr = await http.SendAsync(wh);
            wr.EnsureSuccessStatusCode();
        }

        using (var w = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/wallet"))
        {
            w.Headers.Authorization = lojAuth;
            var wr = await http.SendAsync(w);
            wr.EnsureSuccessStatusCode();
            var wj = await wr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(20, wj.GetProperty("balance_hours").GetInt32());
        }
    }

    [Fact]
    public async Task Lojista_GET_wallet_POST_buy_credito_saldo()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var cs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        var lojId = Guid.NewGuid();
        var lojEmail = $"loj_buy_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.CreateReadWrite(cs);
            db.Lojistas.Add(new LojistaRow { Id = lojId, Name = "Loj E2E", HourPrice = 10m });
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
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = lojEmail, password = "Loj!12345" });
        login.EnsureSuccessStatusCode();
        var lojTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var lojAuth = new AuthenticationHeaderValue("Bearer", lojTok);

        using (var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/buy")
        {
            Content = JsonContent.Create(new { packageId = PkgLoj20h, settlement = "CREDIT" }),
        })
        {
            buy.Headers.Authorization = lojAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var br = await http.SendAsync(buy);
            br.EnsureSuccessStatusCode();
            var bj = await br.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(20, bj.GetProperty("balance_hours").GetInt32());
        }

        using (var w = new HttpRequestMessage(HttpMethod.Get, "/api/v1/lojista/wallet"))
        {
            w.Headers.Authorization = lojAuth;
            var wr = await http.SendAsync(w);
            wr.EnsureSuccessStatusCode();
            var wj = await wr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(20, wj.GetProperty("balance_hours").GetInt32());
        }
    }

    [Fact]
    public async Task Admin_mesmo_parking_POST_operators_unsuspend_operador_ok()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var opId = Guid.NewGuid();
        var opEmail = $"op_uns_{Guid.NewGuid():N}@test.local";

        await using (var scope = fx.Factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            identity.Users.Add(new ParkingIdentityUser
            {
                Id = opId,
                Email = opEmail,
                PasswordHash = Argon2PasswordHasher.Hash("Op!12345"),
                Role = UserRole.OPERATOR,
                ParkingId = parkingId,
                EntityId = null,
                Active = true,
                OperatorSuspended = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await identity.SaveChangesAsync();
        }

        using var uns = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/operators/{opId}/unsuspend");
        uns.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTok);
        var res = await http.SendAsync(uns);
        res.EnsureSuccessStatusCode();
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(j.GetProperty("ok").GetBoolean());

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = opEmail, password = "Op!12345" });
        login.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> PostTicketAsync(HttpClient http, string park, AuthenticationHeaderValue auth, string plate)
    {
        using var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets");
        t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        t1.Headers.Add("X-Parking-Id", park);
        t1.Headers.Authorization = auth;
        t1.Content = JsonContent.Create(new { plate });
        var tr = await http.SendAsync(t1);
        tr.EnsureSuccessStatusCode();
        return (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid paymentId, string amountString)> PostCheckoutAsync(
        HttpClient http,
        string park,
        AuthenticationHeaderValue auth,
        Guid ticketId)
    {
        using var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout");
        t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        t2.Headers.Add("X-Parking-Id", park);
        t2.Headers.Authorization = auth;
        var exitAt = DateTimeOffset.UtcNow.AddHours(3);
        t2.Content = new StringContent(
            JsonSerializer.Serialize(new { exit_time = exitAt }),
            Encoding.UTF8,
            "application/json");
        var cr = await http.SendAsync(t2);
        cr.EnsureSuccessStatusCode();
        var ck = await cr.Content.ReadFromJsonAsync<JsonElement>();
        var paymentId = ck.GetProperty("payment_id").GetGuid();
        var amtStr = ck.GetProperty("amount").GetString()!;
        return (paymentId, amtStr);
    }

    /// <summary>Valores monetários da API podem vir com vírgula (cultura pt-BR no servidor).</summary>
    private static decimal ParseApiMoney(string s) =>
        decimal.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

    private static async Task<string> GetTicketStatusAsync(HttpClient http, string park, AuthenticationHeaderValue auth, Guid ticketId)
    {
        using var t4 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tickets/{ticketId}");
        t4.Headers.Add("X-Parking-Id", park);
        t4.Headers.Authorization = auth;
        var getRes = await http.SendAsync(t4);
        getRes.EnsureSuccessStatusCode();
        var final = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        return final.GetProperty("ticket").GetProperty("status").GetString()!;
    }
}
