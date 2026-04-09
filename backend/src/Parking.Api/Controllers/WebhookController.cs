using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Parking.Domain;
using Parking.Infrastructure.Payments;
using Parking.Api.Parking;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class WebhookController(
    PaymentWebhookSettlement settlement,
    IConfiguration configuration,
    IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    /// <summary>Webhook de desenvolvimento / PSP que replique o contrato interno (HMAC + JSON stub).</summary>
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (!Request.Headers.TryGetValue("X-Signature", out var sigHex))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Assinatura ausente." });

        var secret = configuration["PIX_WEBHOOK_SECRET"] ?? "";
        if (secret.Length < 32)
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Segredo inválido." });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedHex = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        var got = sigHex.ToString();
        if (expectedHex.Length != got.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedHex), Encoding.UTF8.GetBytes(got.ToLowerInvariant())))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Assinatura inválida." });

        var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var transactionId = root.GetProperty("transaction_id").GetString()!;
        var paymentId = root.GetProperty("payment_id").GetGuid();
        var status = root.GetProperty("status").GetString();
        if (status != "PAID")
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Status inválido." });

        var result = await settlement.TryMarkPaidAsync(ParkingId, paymentId, transactionId, PaymentMethod.PIX, ct);
        return result switch
        {
            PaymentWebhookSettlementStatus.Ok => Ok(new { ok = true }),
            PaymentWebhookSettlementStatus.Duplicate => Ok(new { ok = true, duplicate = true }),
            PaymentWebhookSettlementStatus.IgnoredAlreadyPaid => Ok(new { ok = true, ignored = true }),
            PaymentWebhookSettlementStatus.NotFound => NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." }),
            PaymentWebhookSettlementStatus.Late => Conflict(new { code = "WEBHOOK_LATE", message = "Pagamento expirado." }),
            PaymentWebhookSettlementStatus.InvalidState => Conflict(new { code = "INVALID_PAYMENT_STATE", message = "Estado inválido." }),
            _ => StatusCode(500)
        };
    }
}
