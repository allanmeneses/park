using System.Security.Cryptography;
using System.Text;

namespace Parking.Infrastructure.Payments.MercadoPago;

/// <summary>Validação de <c>x-signature</c> conforme documentação Mercado Pago (webhooks).</summary>
public static class MercadoPagoWebhookSignature
{
    /// <summary>
    /// manifest = id:{dataId};request-id:{requestId};ts:{ts};
    /// v1 = HMAC-SHA256(hex) do manifest com o segredo do webhook.
    /// </summary>
    public static bool IsValid(string secret, string? xSignature, string requestId, string dataId, string ts)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(xSignature))
            return false;

        string? v1 = null;
        foreach (var part in xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim() == "v1")
                v1 = kv[1].Trim();
        }

        if (string.IsNullOrEmpty(v1))
            return false;

        var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        var expected = Convert.ToHexStringLower(hash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));
    }

    /// <summary>Extrai ts do header x-signature.</summary>
    public static bool TryGetTs(string? xSignature, out string ts)
    {
        ts = "";
        if (string.IsNullOrEmpty(xSignature))
            return false;
        foreach (var part in xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim() == "ts")
            {
                ts = kv[1].Trim();
                return !string.IsNullOrEmpty(ts);
            }
        }

        return false;
    }
}
