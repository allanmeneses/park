using System.Text.Json;
using Parking.Domain;

namespace Parking.Infrastructure.Payments.MercadoPago;

public sealed record MercadoPagoProviderPaymentSnapshot(
    Guid ParkingPaymentId,
    decimal Amount,
    PaymentMethod Method,
    string Status,
    string? StatusDetail);

public static class MercadoPagoNotificationParser
{
    /// <summary>
    /// Mercado Pago assina o webhook com <c>data.id</c> vindo da <strong>query string</strong>
    /// (<c>data.id</c>); o corpo pode repetir o id ou estar mínimo. Prioriza query, depois JSON.
    /// </summary>
    public static bool TryGetWebhookDataId(string? dataIdFromQuery, string json, out string dataId)
    {
        if (!string.IsNullOrWhiteSpace(dataIdFromQuery))
        {
            dataId = dataIdFromQuery.Trim();
            return true;
        }

        return TryGetDataId(json, out dataId);
    }

    /// <summary>
    /// Documentação MP: valores alfanuméricos de <c>data.id</c> na URL devem ir em minúsculas no manifest.
    /// </summary>
    public static string NormalizeDataIdForWebhookSignature(string dataId)
    {
        if (string.IsNullOrEmpty(dataId))
            return dataId;
        foreach (var c in dataId)
        {
            if (char.IsLetter(c))
                return dataId.ToLowerInvariant();
        }

        return dataId;
    }

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

    /// <summary>Lê o campo <c>status</c> de GET /v1/payments/{id} (resposta Mercado Pago).</summary>
    public static string? GetMercadoPagoApiPaymentStatus(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : null;
    }

    /// <summary>
    /// Estados em que o PSP ainda pode transicionar para <c>approved</c>; vale a pena reconsultar antes de ignorar o webhook.
    /// </summary>
    public static bool IsRetryableMercadoPagoPaymentState(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.Equals("pending", StringComparison.OrdinalIgnoreCase)
               || status.Equals("in_process", StringComparison.OrdinalIgnoreCase)
               || status.Equals("authorized", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Interpreta GET /v1/payments/{id} quando status approved.</summary>
    public static bool TryParseApprovedPayment(string json, out Guid parkingPaymentId, out decimal amount, out PaymentMethod method)
    {
        if (!TryParsePaymentSnapshot(json, out var snapshot) ||
            !string.Equals(snapshot.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            parkingPaymentId = default;
            amount = 0;
            method = PaymentMethod.CARD;
            return false;
        }

        parkingPaymentId = snapshot.ParkingPaymentId;
        amount = snapshot.Amount;
        method = snapshot.Method;
        return true;
    }

    public static bool TryParsePaymentSnapshot(string json, out MercadoPagoProviderPaymentSnapshot snapshot)
    {
        snapshot = default!;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        if (!root.TryGetProperty("external_reference", out var er) ||
            !Guid.TryParse(er.GetString(), out var parkingPaymentId))
            return false;

        if (!root.TryGetProperty("transaction_amount", out var ta))
            return false;
        var amount = ta.GetDecimal();

        var pm = root.TryGetProperty("payment_method_id", out var pmi) ? pmi.GetString() : null;
        var method = string.Equals(pm, "pix", StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.PIX
            : PaymentMethod.CARD;
        var statusDetail = root.TryGetProperty("status_detail", out var sd) ? sd.GetString() : null;

        snapshot = new MercadoPagoProviderPaymentSnapshot(
            parkingPaymentId,
            amount,
            method,
            status ?? "",
            statusDetail);
        return true;
    }
}
