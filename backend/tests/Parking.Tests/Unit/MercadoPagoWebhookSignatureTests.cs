using Parking.Infrastructure.Payments.MercadoPago;

namespace Parking.Tests.Unit;

public sealed class MercadoPagoWebhookSignatureTests
{
    [Fact]
    public void IsValid_aceita_assinatura_correta()
    {
        const string secret = "supersecretwebhookkeymustbe32chars!!";
        const string requestId = "req-1";
        const string dataId = "12345";
        const string ts = "1700000000";
        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var v1 = Convert.ToHexStringLower(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(manifest)));
        var xSig = $"ts={ts},v1={v1}";
        Assert.True(MercadoPagoWebhookSignature.IsValid(secret, xSig, requestId, dataId, ts));
    }

    [Fact]
    public void IsValid_rejeita_secret_errado()
    {
        const string ts = "1";
        var xSig = "ts=1,v1=deadbeef";
        Assert.False(MercadoPagoWebhookSignature.IsValid("wrongsecretwrongsecretwrongsecretx", xSig, "r", "1", ts));
    }

    [Fact]
    public void TryGetDataId_lê_data_id()
    {
        Assert.True(MercadoPagoNotificationParser.TryGetDataId("""{"data":{"id":"999"}}""", out var id));
        Assert.Equal("999", id);
    }
}
