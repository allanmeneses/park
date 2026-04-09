using Parking.Infrastructure.Payments;
using Parking.Infrastructure.Payments.MercadoPago;

namespace Parking.Tests.Unit;

public sealed class MercadoPagoApiParserTests
{
    [Fact]
    public void ParsePixPaymentResponse_extrai_qr_code_e_id()
    {
        const string json = """
            {"id":999888,"status":"pending","date_of_expiration":"2030-01-15T12:00:00.000-00:00","point_of_interaction":{"transaction_data":{"qr_code":"00020101021226800014br.gov.bcb.pix"}}}
            """;
        var r = MercadoPagoApiParser.ParsePixPaymentResponse(json, Guid.Empty);
        Assert.Equal("00020101021226800014br.gov.bcb.pix", r.QrCode);
        Assert.Equal("999888", r.ProviderTransactionId);
    }

    [Fact]
    public void ParsePreferenceResponse_extrai_init_point()
    {
        const string json = """
            {"id":"PREF123","init_point":"https://www.mercadopago.com.br/checkout/v1/redirect?pref_id=PREF123","sandbox_init_point":"https://sandbox.mercadopago.com.br/checkout/v1/redirect?pref_id=PREF123"}
            """;
        var r = MercadoPagoApiParser.ParsePreferenceResponse(json, "APP_USR-pub");
        Assert.Equal("PREF123", r.PreferenceId);
        Assert.Contains("PREF123", r.InitPointUrl);
        Assert.Equal("APP_USR-pub", r.PublicKey);
    }
}
