using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Tenants;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Auth;

[Collection(nameof(PostgresApiCollection))]
public sealed class ClientRegisterIntegrationTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Public_client_register_returns_tokens_and_creates_wallet()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);
        var email = $"cli_reg_{Guid.NewGuid():N}@test.local";

        var reg = await http.PostAsJsonAsync("/api/v1/auth/register-client", new
        {
            parkingId,
            plate = "abc1d23",
            email,
            password = "CliReg!12345",
        });

        reg.EnsureSuccessStatusCode();
        var tok = await reg.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(tok.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(tok.GetProperty("refresh_token").GetString()));

        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "CliReg!12345" });
        login.EnsureSuccessStatusCode();
        var cliTok = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        using (var wallet = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client/wallet"))
        {
            wallet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cliTok);
            var res = await http.SendAsync(wallet);
            res.EnsureSuccessStatusCode();
            var j = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, j.GetProperty("balance_hours").GetInt32());
        }

        await using var scope = fx.Factory.Services.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")!;
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        var tenantCs = TenantConnectionStringBuilder.FromTemplate(template, parkingId);
        await using var tdb = tenantFactory.CreateReadWrite(tenantCs);

        var user = await identity.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(Parking.Domain.UserRole.CLIENT, user.Role);
        Assert.Equal(parkingId, user.ParkingId);
        Assert.NotNull(user.EntityId);

        var client = await tdb.Clients.SingleAsync(c => c.Id == user.EntityId);
        Assert.Equal("ABC1D23", client.Plate);

        var walletRow = await tdb.ClientWallets.SingleAsync(w => w.ClientId == client.Id);
        Assert.Equal(0, walletRow.BalanceHours);
    }

    [Fact]
    public async Task Register_client_with_invalid_plate_returns_PLATE_INVALID()
    {
        var http = fx.Factory.CreateClient();
        var (parkingId, _) = await E2ETenantProvision.NewTenantWithAdminAsync(http);

        var reg = await http.PostAsJsonAsync("/api/v1/auth/register-client", new
        {
            parkingId,
            plate = "123",
            email = $"cli_bad_{Guid.NewGuid():N}@test.local",
            password = "CliReg!12345",
        });

        Assert.Equal(HttpStatusCode.BadRequest, reg.StatusCode);
        var j = await reg.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PLATE_INVALID", j.GetProperty("code").GetString());
    }
}
