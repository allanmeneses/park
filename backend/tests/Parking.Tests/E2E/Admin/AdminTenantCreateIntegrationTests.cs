using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Admin;

/// <summary>Provisionamento de tenant: só SUPER_ADMIN; corpo exige admin + operador distintos.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class AdminTenantCreateIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Post_tenants_missing_operator_email_returns_400()
    {
        var http = fx.Factory.CreateClient();
        var superTok = await LoginSuperAsync(http);

        var tenantReq = new
        {
            parkingId = (Guid?)null,
            adminEmail = $"adm_miss_{Guid.NewGuid():N}@test.local",
            adminPassword = "Admin!12345",
            operatorEmail = (string?)null,
            operatorPassword = (string?)null,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants") { Content = JsonContent.Create(tenantReq) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_tenants_same_admin_and_operator_email_returns_400()
    {
        var http = fx.Factory.CreateClient();
        var superTok = await LoginSuperAsync(http);
        var email = $"same_{Guid.NewGuid():N}@test.local";
        var tenantReq = new
        {
            parkingId = (Guid?)null,
            adminEmail = email,
            adminPassword = "Admin!12345",
            operatorEmail = email,
            operatorPassword = "Op!12345",
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants") { Content = JsonContent.Create(tenantReq) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Post_tenants_as_tenant_ADMIN_returns_403()
    {
        var http = fx.Factory.CreateClient();
        var (_, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);

        var tenantReq = new
        {
            parkingId = (Guid?)null,
            adminEmail = $"other_adm_{Guid.NewGuid():N}@test.local",
            adminPassword = "Admin!12345",
            operatorEmail = $"other_op_{Guid.NewGuid():N}@test.local",
            operatorPassword = "Op!12345",
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants") { Content = JsonContent.Create(tenantReq) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTok);
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Post_tenants_creates_admin_and_operator_both_can_login()
    {
        var http = fx.Factory.CreateClient();
        var superTok = await LoginSuperAsync(http);
        var adm = $"adm_ok_{Guid.NewGuid():N}@test.local";
        var op = $"op_ok_{Guid.NewGuid():N}@test.local";

        var tenantReq = new
        {
            parkingId = (Guid?)null,
            adminEmail = adm,
            adminPassword = "Admin!12345",
            operatorEmail = op,
            operatorPassword = "Op!12345",
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants") { Content = JsonContent.Create(tenantReq) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var tenantRes = await http.SendAsync(req);
        tenantRes.EnsureSuccessStatusCode();
        var tenant = await tenantRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(tenant.TryGetProperty("parking_id", out _) || tenant.TryGetProperty("parkingId", out _));

        var loginAdm = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = adm, password = "Admin!12345" });
        loginAdm.EnsureSuccessStatusCode();
        var ad = await loginAdm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(ad.GetProperty("access_token").GetString()));

        var loginOp = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = op, password = "Op!12345" });
        loginOp.EnsureSuccessStatusCode();
        var od = await loginOp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(od.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Get_tenants_as_tenant_ADMIN_returns_403()
    {
        var http = fx.Factory.CreateClient();
        var (_, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/tenants");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTok);
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private static async Task<string> LoginSuperAsync(HttpClient http)
    {
        var loginSuper = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        loginSuper.EnsureSuccessStatusCode();
        return (await loginSuper.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
    }
}
