using System.Text.Json;

namespace Parking.Infrastructure.Payments.MercadoPago;

/// <summary>Parser puro para testes e para o cliente HTTP (sem I/O).</summary>
public static class MercadoPagoApiParser
{
    public static PixChargeResult ParsePixPaymentResponse(string json, Guid paymentIdFallback)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl)
            ? idEl.GetRawText().Trim('"')
            : paymentIdFallback.ToString("N");

        DateTimeOffset expiresAt;
        if (root.TryGetProperty("date_of_expiration", out var expEl) &&
            DateTimeOffset.TryParse(expEl.GetString(), out var parsed))
            expiresAt = parsed.ToUniversalTime();
        else
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(30);

        var qr = "";
        if (root.TryGetProperty("point_of_interaction", out var poi) &&
            poi.TryGetProperty("transaction_data", out var td) &&
            td.TryGetProperty("qr_code", out var qrEl))
            qr = qrEl.GetString() ?? "";

        if (string.IsNullOrWhiteSpace(qr))
            throw new InvalidOperationException("Mercado Pago: resposta Pix sem qr_code.");

        return new PixChargeResult(qr, expiresAt, id);
    }

    public static CardCheckoutSession ParsePreferenceResponse(string json, string publicKey)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var prefId = root.GetProperty("id").GetString() ?? throw new InvalidOperationException("Preference sem id.");
        var init = root.TryGetProperty("init_point", out var ip) ? ip.GetString() ?? "" : "";
        var sand = root.TryGetProperty("sandbox_init_point", out var sp) ? sp.GetString() : null;
        if (string.IsNullOrEmpty(init))
            throw new InvalidOperationException("Mercado Pago: preference sem init_point.");
        return new CardCheckoutSession(prefId, init, sand, string.IsNullOrEmpty(publicKey) ? null : publicKey);
    }
}
