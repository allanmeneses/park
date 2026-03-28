using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Tickets;

/// <summary>SPEC §24 <c>Tickets</c> — contratos §18 na borda HTTP.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class TicketsContractIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Get_open_sem_JWT_retorna_401()
    {
        var http = fx.Factory.CreateClient();
        var r = await http.GetAsync("/api/v1/tickets/open");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Post_create_sem_Idempotency_Key_retorna_400()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets")
        {
            Content = JsonContent.Create(new { plate = "ABC1D23" }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTok);
        req.Headers.Add("X-Parking-Id", parkingId.ToString());
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_ERROR", j.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_create_placa_invalida_retorna_PLATE_INVALID()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, adminTok) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets")
        {
            Content = JsonContent.Create(new { plate = "INVALID" }),
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTok);
        req.Headers.Add("X-Parking-Id", parkingId.ToString());
        var res = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PLATE_INVALID", j.GetProperty("code").GetString());
    }
}
