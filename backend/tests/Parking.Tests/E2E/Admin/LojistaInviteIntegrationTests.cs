using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Admin;

/// <summary>Convites de lojista: criação por ADMIN/SUPER_ADMIN e auto cadastro público.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class LojistaInviteIntegrationTests(PostgresWebAppFixture fx)
{
    private static readonly Guid PkgLoj20h = Guid.Parse("22222222-2222-2222-2222-222222222201");

    [Fact]
    public async Task Admin_creates_invite_list_and_lojista_registers_with_tokens()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        string merchantCode;
        string activationCode;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/lojista-invites")
        {
            Content = JsonContent.Create(new { displayName = "Loja Alfa" }),
        })
        {
            create.Headers.Authorization = auth;
            create.Headers.Add("X-Parking-Id", park);
            var res = await http.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            merchantCode = j.GetProperty("merchantCode").GetString()!;
            activationCode = j.GetProperty("activationCode").GetString()!;
            Assert.Equal(10, merchantCode.Length);
            Assert.True(activationCode.Length >= 12);
        }

        using (var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/lojista-invites"))
        {
            list.Headers.Authorization = auth;
            list.Headers.Add("X-Parking-Id", park);
            var res = await http.SendAsync(list);
            res.EnsureSuccessStatusCode();
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            var items = j.GetProperty("items");
            Assert.True(items.GetArrayLength() >= 1);
            var first = items[0];
            Assert.Equal(merchantCode, first.GetProperty("merchantCode").GetString());
            Assert.Equal("Loja Alfa", first.GetProperty("shopName").GetString());
            Assert.False(first.GetProperty("activated").GetBoolean());
            Assert.Equal(JsonValueKind.Null, first.GetProperty("email").ValueKind);
            Assert.Equal(JsonValueKind.Null, first.GetProperty("totalPurchasedHours").ValueKind);
            Assert.Equal(JsonValueKind.Null, first.GetProperty("balanceHours").ValueKind);
        }

        var email = $"loj_reg_{Guid.NewGuid():N}@test.local";
        var reg = await http.PostAsJsonAsync("/api/v1/auth/register-lojista", new
        {
            merchantCode,
            activationCode,
            email,
            password = "Lojista!12345",
            name = "Nome Oficial",
        });
        reg.EnsureSuccessStatusCode();
        var tok = await reg.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(tok.GetProperty("access_token").GetString()));

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Lojista!12345" });
        login.EnsureSuccessStatusCode();
        var lojTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        var lojAuth = new AuthenticationHeaderValue("Bearer", lojTok);

        using (var list2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/lojista-invites"))
        {
            list2.Headers.Authorization = auth;
            list2.Headers.Add("X-Parking-Id", park);
            var res = await http.SendAsync(list2);
            res.EnsureSuccessStatusCode();
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            var row = j.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("merchantCode").GetString() == merchantCode);
            Assert.True(row.GetProperty("activated").GetBoolean());
            Assert.Equal(email, row.GetProperty("email").GetString());
            Assert.Equal("Nome Oficial", row.GetProperty("shopName").GetString());
            Assert.Equal(0, row.GetProperty("totalPurchasedHours").GetInt32());
            Assert.Equal(0, row.GetProperty("balanceHours").GetInt32());
        }

        using (var buy = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lojista/buy")
        {
            Content = JsonContent.Create(new { packageId = PkgLoj20h, settlement = "CREDIT" }),
        })
        {
            buy.Headers.Authorization = lojAuth;
            buy.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var br = await http.SendAsync(buy);
            br.EnsureSuccessStatusCode();
        }

        using (var list3 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/lojista-invites"))
        {
            list3.Headers.Authorization = auth;
            list3.Headers.Add("X-Parking-Id", park);
            var res = await http.SendAsync(list3);
            res.EnsureSuccessStatusCode();
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            var row = j.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("merchantCode").GetString() == merchantCode);
            Assert.Equal(20, row.GetProperty("totalPurchasedHours").GetInt32());
            Assert.Equal(20, row.GetProperty("balanceHours").GetInt32());
        }
    }

    [Fact]
    public async Task Register_wrong_activation_returns_LOJISTA_INVITE_INVALID()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        JsonElement created;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/lojista-invites")
        {
            Content = JsonContent.Create(new { }),
        })
        {
            create.Headers.Authorization = auth;
            create.Headers.Add("X-Parking-Id", park);
            var res = await http.SendAsync(create);
            res.EnsureSuccessStatusCode();
            created = await res.Content.ReadFromJsonAsync<JsonElement>();
        }

        var reg = await http.PostAsJsonAsync("/api/v1/auth/register-lojista", new
        {
            merchantCode = created.GetProperty("merchantCode").GetString(),
            activationCode = "ZZZZZZZZZZZZ",
            email = $"bad_{Guid.NewGuid():N}@test.local",
            password = "Lojista!12345",
            name = "X",
        });
        Assert.Equal(HttpStatusCode.BadRequest, reg.StatusCode);
        var j = await reg.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOJISTA_INVITE_INVALID", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_twice_same_invite_second_returns_LOJISTA_INVITE_CONSUMED()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var park = parkingId.ToString();
        var auth = new AuthenticationHeaderValue("Bearer", adminTok);

        JsonElement created;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/lojista-invites")
        {
            Content = JsonContent.Create(new { }),
        })
        {
            create.Headers.Authorization = auth;
            create.Headers.Add("X-Parking-Id", park);
            var res = await http.SendAsync(create);
            res.EnsureSuccessStatusCode();
            created = await res.Content.ReadFromJsonAsync<JsonElement>();
        }

        var merchantCode = created.GetProperty("merchantCode").GetString()!;
        var activationCode = created.GetProperty("activationCode").GetString()!;
        var body = new
        {
            merchantCode,
            activationCode,
            email = $"two_{Guid.NewGuid():N}@test.local",
            password = "Lojista!12345",
            name = "Primeiro",
        };
        var first = await http.PostAsJsonAsync("/api/v1/auth/register-lojista", body);
        first.EnsureSuccessStatusCode();

        var second = await http.PostAsJsonAsync("/api/v1/auth/register-lojista", new
        {
            merchantCode,
            activationCode,
            email = $"two_b_{Guid.NewGuid():N}@test.local",
            password = "Lojista!12345",
            name = "Segundo",
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var j = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LOJISTA_INVITE_CONSUMED", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Operator_cannot_create_lojista_invite_403()
    {
        var http = fx.Factory.CreateClient();

        // tenant creation response has operator email in E2ETenantProvision - we need operator token
        var loginSuper = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        loginSuper.EnsureSuccessStatusCode();
        var superTok = (await loginSuper.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        var tenantReq = new
        {
            parkingId = (Guid?)null,
            adminEmail = $"adm_opinv_{Guid.NewGuid():N}@test.local",
            adminPassword = "Admin!12345",
            operatorEmail = $"op_inv_{Guid.NewGuid():N}@test.local",
            operatorPassword = "Op!12345",
        };
        using var reqT = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants") { Content = JsonContent.Create(tenantReq) };
        reqT.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var tenantRes = await http.SendAsync(reqT);
        tenantRes.EnsureSuccessStatusCode();
        var tenant = await tenantRes.Content.ReadFromJsonAsync<JsonElement>();
        var pid = tenant.TryGetProperty("parkingId", out var pidEl)
            ? pidEl.GetGuid()
            : tenant.GetProperty("parking_id").GetGuid();

        var loginOp = await http.PostAsJsonAsync("/api/v1/auth/login",
            new { email = tenantReq.operatorEmail, password = tenantReq.operatorPassword });
        loginOp.EnsureSuccessStatusCode();
        var opTok = (await loginOp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using var inv = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/lojista-invites")
        {
            Content = JsonContent.Create(new { }),
        };
        inv.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opTok);
        inv.Headers.Add("X-Parking-Id", pid.ToString());
        var res = await http.SendAsync(inv);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
