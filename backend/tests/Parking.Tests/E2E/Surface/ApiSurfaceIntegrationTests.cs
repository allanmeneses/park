using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Surface;

/// <summary>SPEC §24 <c>Surface</c> — caminhos felizes e 4xx (§18 / §23.2): Auth, Settings, Dashboard, Cash, Webhook, Payments.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class ApiSurfaceIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Auth_login_credenciais_erradas_401()
    {
        var http = fx.Factory.CreateClient();
        var res = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "nope@test.local", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Auth_refresh_token_invalido_401()
    {
        var http = fx.Factory.CreateClient();
        var res = await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "invalid-opaque" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Auth_logout_retorna_ok()
    {
        var http = fx.Factory.CreateClient();
        var res = await http.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "anything" });
        res.EnsureSuccessStatusCode();
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(j.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Settings_sem_JWT_401()
    {
        var http = fx.Factory.CreateClient();
        var res = await http.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Gestor_settings_dashboard_cash_operator_problem_fluxo_minimo()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);
        var park = parkingId.ToString();

        using (var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/settings"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
            var s = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(s.TryGetProperty("price_per_hour", out _));
            Assert.True(s.TryGetProperty("capacity", out _));
        }

        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/settings"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            req.Content = JsonContent.Create(new { pricePerHour = 7.5m, capacity = 42 });
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
        }

        using (var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
            var d = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(d.TryGetProperty("faturamento", out _));
            Assert.True(d.TryGetProperty("ocupacao", out _));
            Assert.True(d.TryGetProperty("tickets_dia", out _));
        }

        using (var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cash"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
        }

        Guid sessionId;
        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/open"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
            var o = await r.Content.ReadFromJsonAsync<JsonElement>();
            sessionId = o.GetProperty("session_id").GetGuid();
        }

        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/close"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            req.Content = JsonContent.Create(new { sessionId, actualAmount = 0.00m });
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
        }

        using (var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/recharge-packages?scope=CLIENT"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
            var p = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(p.TryGetProperty("items", out _));
        }

        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/operator/problem"))
        {
            req.Headers.Authorization = auth;
            req.Headers.Add("X-Parking-Id", park);
            req.Content = JsonContent.Create(new { });
            var r = await http.SendAsync(req);
            r.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Webhook_assinatura_errada_401()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var body = """{"transaction_id":"x","payment_id":"00000000-0000-0000-0000-000000000001","status":"PAID"}""";
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Parking-Id", parkingId.ToString());
        req.Headers.Add("X-Signature", "deadbeef");
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("WEBHOOK_SIGNATURE_INVALID", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_payment_por_id_apos_checkout()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        Guid ticketId;
        using (var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets"))
        {
            t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t1.Headers.Add("X-Parking-Id", park);
            t1.Headers.Authorization = auth;
            t1.Content = JsonContent.Create(new { plate = "XYZ9A87" });
            var tr = await http.SendAsync(t1);
            tr.EnsureSuccessStatusCode();
            ticketId = (await tr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        Guid paymentId;
        using (var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout"))
        {
            t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            t2.Headers.Add("X-Parking-Id", park);
            t2.Headers.Authorization = auth;
            t2.Content = JsonContent.Create(new { });
            var cr = await http.SendAsync(t2);
            cr.EnsureSuccessStatusCode();
            var ck = await cr.Content.ReadFromJsonAsync<JsonElement>();
            paymentId = ck.GetProperty("payment_id").GetGuid();
        }

        using (var g = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/payments/{paymentId}"))
        {
            g.Headers.Authorization = auth;
            g.Headers.Add("X-Parking-Id", park);
            var pr = await http.SendAsync(g);
            pr.EnsureSuccessStatusCode();
            var pay = await pr.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(paymentId.ToString(), pay.GetProperty("id").GetString());
            Assert.True(pay.TryGetProperty("status", out _));
            Assert.True(pay.TryGetProperty("amount", out _));
        }
    }
}
