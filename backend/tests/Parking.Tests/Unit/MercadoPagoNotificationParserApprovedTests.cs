using Parking.Domain;
using Parking.Infrastructure.Payments.MercadoPago;

namespace Parking.Tests.Unit;

public sealed class MercadoPagoNotificationParserApprovedTests
{
    [Fact]
    public void TryParseApprovedPayment_pix()
    {
        var pid = Guid.NewGuid();
        var json = $$"""
            {"status":"approved","external_reference":"{{pid:D}}","transaction_amount":12.50,"payment_method_id":"pix"}
            """;
        Assert.True(MercadoPagoNotificationParser.TryParseApprovedPayment(json, out var id, out var amt, out var m));
        Assert.Equal(pid, id);
        Assert.Equal(12.50m, amt);
        Assert.Equal(PaymentMethod.PIX, m);
    }

    [Fact]
    public void TryParseApprovedPayment_cartao()
    {
        var pid = Guid.NewGuid();
        var json = $$"""
            {"status":"approved","external_reference":"{{pid:D}}","transaction_amount":10,"payment_method_id":"visa"}
            """;
        Assert.True(MercadoPagoNotificationParser.TryParseApprovedPayment(json, out _, out _, out var m));
        Assert.Equal(PaymentMethod.CARD, m);
    }
}
