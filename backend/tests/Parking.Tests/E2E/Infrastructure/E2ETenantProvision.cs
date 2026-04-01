using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Parking.Tests.E2E.Infrastructure;

internal static class E2ETenantProvision
{
    internal static async Task<(Guid parkingId, string adminToken)> NewTenantWithAdminAsync(HttpClient http)
    {
        var loginSuper = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        loginSuper.EnsureSuccessStatusCode();
        var superTok = (await loginSuper.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        var tenantReq = new
        {
            parkingId = (Guid?)null,
            adminEmail = $"e2e_admin_{Guid.NewGuid():N}@test.local",
            adminPassword = "Admin!12345",
            operatorEmail = $"e2e_op_{Guid.NewGuid():N}@test.local",
            operatorPassword = "Op!12345",
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants") { Content = JsonContent.Create(tenantReq) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var tenantRes = await http.SendAsync(req);
        tenantRes.EnsureSuccessStatusCode();
        var tenant = await tenantRes.Content.ReadFromJsonAsync<JsonElement>();
        var parkingId = tenant.TryGetProperty("parkingId", out var pidEl)
            ? pidEl.GetGuid()
            : tenant.GetProperty("parking_id").GetGuid();

        var loginAdmin = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = tenantReq.adminEmail, password = tenantReq.adminPassword });
        loginAdmin.EnsureSuccessStatusCode();
        var adminTok = (await loginAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        return (parkingId, adminTok);
    }
}
