using System.Text.Json;
using Parking.Domain;

namespace Parking.Infrastructure.Payments.MercadoPago;

public static class MercadoPagoNotificationParser
{
    public static bool TryGetDataId(string json, out string dataId)
    {
        dataId = "";
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var idEl))
        {
            dataId = idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString() ?? ""
                : idEl.GetRawText().Trim('"');
            return !string.IsNullOrEmpty(dataId);
        }

        if (!root.TryGetProperty("resource", out var res))
            return false;
        var s = res.GetString();
        if (string.IsNullOrEmpty(s))
            return false;
        if (long.TryParse(s, out _))
        {
            dataId = s;
            return true;
        }

        var trimmed = s.TrimEnd('/');
        var last = trimmed.Split('/').LastOrDefault();
        if (!string.IsNullOrEmpty(last) && long.TryParse(last, out _))
        {
            dataId = last;
            return true;
        }

        return false;
    }

    /// <summary>Interpreta GET /v1/payments/{id} quando status approved.</summary>
    public static bool TryParseApprovedPayment(string json, out Guid parkingPaymentId, out decimal amount, out PaymentMethod method)
    {
        parkingPaymentId = default;
        amount = 0;
        method = PaymentMethod.CARD;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        if (!string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!root.TryGetProperty("external_reference", out var er) ||
            !Guid.TryParse(er.GetString(), out parkingPaymentId))
            return false;

        if (!root.TryGetProperty("transaction_amount", out var ta))
            return false;
        amount = ta.GetDecimal();

        var pm = root.TryGetProperty("payment_method_id", out var pmi) ? pmi.GetString() : null;
        method = string.Equals(pm, "pix", StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.PIX
            : PaymentMethod.CARD;

        return true;
    }
}
