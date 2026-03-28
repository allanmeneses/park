using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Parking.Tests.E2E.Infrastructure;
using Xunit;

namespace Parking.Tests.E2E.Flows;

/// <summary>SPEC §24 <c>Flows</c> — E2E Payments + Webhook + Tickets.</summary>
[Collection(nameof(PostgresApiCollection))]
public sealed class E2EFlowTests(PostgresWebAppFixture fx)
{
    [Fact]
    public async Task Health_ok()
    {
        var client = fx.Factory.CreateClient();
        var r = await client.GetAsync("/health");
        r.EnsureSuccessStatusCode();
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task E2E_Checkout_Pix_Webhook_ClosesTicket()
    {
        var client = fx.Factory.CreateClient();

        var loginSuper = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "super@test.com", password = "Super!12345" });
        loginSuper.EnsureSuccessStatusCode();
        var superTok = (await loginSuper.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        var tenantReq = new { parkingId = (Guid?)null, adminEmail = "admin@test.com", adminPassword = "Admin!12345" };
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants")
        {
            Content = JsonContent.Create(tenantReq)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", superTok);
        var tenantRes = await client.SendAsync(req);
        var tenantBody = await tenantRes.Content.ReadAsStringAsync();
        Assert.True(tenantRes.IsSuccessStatusCode, $"POST tenants failed: {(int)tenantRes.StatusCode} {tenantBody}");
        var tenant = await tenantRes.Content.ReadFromJsonAsync<JsonElement>();
        var parkingId = tenant.GetProperty("parking_id").GetGuid();

        var loginAdmin = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@test.com", password = "Admin!12345" });
        loginAdmin.EnsureSuccessStatusCode();
        var adminJson = await loginAdmin.Content.ReadFromJsonAsync<JsonElement>();
        var adminToken = adminJson.GetProperty("access_token").GetString()!;

        using var t1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tickets");
        t1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        t1.Headers.Add("X-Parking-Id", parkingId.ToString());
        t1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        t1.Content = JsonContent.Create(new { plate = "ABC1D23" });
        var ticketRes = await client.SendAsync(t1);
        ticketRes.EnsureSuccessStatusCode();
        var ticket = await ticketRes.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = ticket.GetProperty("id").GetGuid();

        using var t2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/tickets/{ticketId}/checkout");
        t2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        t2.Headers.Add("X-Parking-Id", parkingId.ToString());
        t2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        t2.Content = JsonContent.Create(new { exit_time = DateTimeOffset.UtcNow.AddHours(2) });
        var checkoutRes = await client.SendAsync(t2);
        var checkoutTxt = await checkoutRes.Content.ReadAsStringAsync();
        Assert.True(checkoutRes.IsSuccessStatusCode, $"checkout: {(int)checkoutRes.StatusCode} {checkoutTxt}");
        var checkout = JsonSerializer.Deserialize<JsonElement>(checkoutTxt);
        var paymentId = checkout.GetProperty("payment_id").GetGuid();

        using var t3 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/pix");
        t3.Headers.Add("X-Parking-Id", parkingId.ToString());
        t3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        t3.Content = JsonContent.Create(new { payment_id = paymentId });
        var pixRes = await client.SendAsync(t3);
        pixRes.EnsureSuccessStatusCode();
        await pixRes.Content.ReadFromJsonAsync<JsonElement>();

        var webhookBody = JsonSerializer.Serialize(new { transaction_id = Guid.NewGuid().ToString("N"), payment_id = paymentId, status = "PAID" });
        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(new string('b', 32)));
        var sig = Convert.ToHexStringLower(mac.ComputeHash(Encoding.UTF8.GetBytes(webhookBody)));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(webhookBody, Encoding.UTF8, "application/json")
        };
        wh.Headers.Add("X-Parking-Id", parkingId.ToString());
        wh.Headers.Add("X-Signature", sig);
        var whRes = await client.SendAsync(wh);
        whRes.EnsureSuccessStatusCode();

        using var t4 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/tickets/{ticketId}");
        t4.Headers.Add("X-Parking-Id", parkingId.ToString());
        t4.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var getRes = await client.SendAsync(t4);
        getRes.EnsureSuccessStatusCode();
        var final = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CLOSED", final.GetProperty("ticket").GetProperty("status").GetString());
    }
}
