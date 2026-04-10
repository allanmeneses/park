using Parking.Infrastructure.Payments.MercadoPago;

namespace Parking.Tests.Unit;

public sealed class MercadoPagoNotificationParserWebhookTests
{
    [Fact]
    public void TryGetWebhookDataId_prioriza_query()
    {
        Assert.True(MercadoPagoNotificationParser.TryGetWebhookDataId(" 777 ", """{"data":{"id":"999"}}""", out var id));
        Assert.Equal("777", id);
    }

    [Fact]
    public void TryGetWebhookDataId_sem_query_usa_corpo()
    {
        Assert.True(MercadoPagoNotificationParser.TryGetWebhookDataId(null, """{"data":{"id":"888"}}""", out var id));
        Assert.Equal("888", id);
    }

    [Fact]
    public void TryGetWebhookDataId_query_vazia_usa_corpo()
    {
        Assert.True(MercadoPagoNotificationParser.TryGetWebhookDataId("   ", """{"data":{"id":"888"}}""", out var id));
        Assert.Equal("888", id);
    }

    [Fact]
    public void NormalizeDataIdForWebhookSignature_minúsculas_quando_há_letras()
    {
        Assert.Equal("ab12", MercadoPagoNotificationParser.NormalizeDataIdForWebhookSignature("Ab12"));
    }

    [Fact]
    public void NormalizeDataIdForWebhookSignature_só_dígitos_inalterado()
    {
        Assert.Equal("12345", MercadoPagoNotificationParser.NormalizeDataIdForWebhookSignature("12345"));
    }

    [Fact]
    public void GetMercadoPagoApiPaymentStatus_lê_status()
    {
        Assert.Equal("in_process", MercadoPagoNotificationParser.GetMercadoPagoApiPaymentStatus("""{"status":"in_process"}"""));
    }

    [Theory]
    [InlineData("pending", true)]
    [InlineData("in_process", true)]
    [InlineData("authorized", true)]
    [InlineData("approved", false)]
    [InlineData("rejected", false)]
    public void IsRetryableMercadoPagoPaymentState(string status, bool expected)
    {
        Assert.Equal(expected, MercadoPagoNotificationParser.IsRetryableMercadoPagoPaymentState(status));
    }
}
