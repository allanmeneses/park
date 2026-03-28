using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Parking.Tests.E2E.Infrastructure;

/// <summary>SPEC §2.1 / §70 — 503 TENANT_UNAVAILABLE quando o banco do tenant não conecta.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class TenantUnavailableIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Super_admin_X_Parking_Id_sem_banco_provisionado_retorna_503_TENANT_UNAVAILABLE()
    {
        var http = fx.Factory.CreateClient();
        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        login.EnsureSuccessStatusCode();
        var tok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        var semDb = Guid.Parse("f0000000-0000-4000-8000-000000000001");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/settings");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        req.Headers.Add("X-Parking-Id", semDb.ToString());

        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("TENANT_UNAVAILABLE", body.GetProperty("code").GetString());
        Assert.Contains("indispon", body.GetProperty("message").GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
