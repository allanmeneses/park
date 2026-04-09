using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Parking.Domain;
using Parking.Infrastructure.Payments;
using Parking.Infrastructure.Payments.MercadoPago;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/payments/webhook/psp")]
public sealed class MercadoPagoWebhookController(
    TenantDbContext db,
    PaymentWebhookSettlement settlement,
    IHttpClientFactory httpClientFactory,
    IOptions<MercadoPagoOptions> options) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("mercadopago/{parkingId:guid}")]
    public async Task<IActionResult> MercadoPago([FromRoute] Guid parkingId, CancellationToken ct)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.WebhookSecret))
            return Unauthorized(new { code = "WEBHOOK_MISCONFIGURED", message = "MERCADOPAGO_WEBHOOK_SECRET ausente." });

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (!Request.Headers.TryGetValue("x-signature", out var xSig))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "x-signature ausente." });
        if (!Request.Headers.TryGetValue("x-request-id", out var xReq))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "x-request-id ausente." });

        var xSigStr = xSig.ToString().Trim();
        var xReqStr = xReq.ToString().Trim();
        if (string.IsNullOrEmpty(xSigStr))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "x-signature ausente." });
        if (string.IsNullOrEmpty(xReqStr))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "x-request-id ausente." });

        var qid = Request.Query["data.id"].FirstOrDefault();
        if (!MercadoPagoNotificationParser.TryGetWebhookDataId(qid, raw, out var dataId))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Notificação sem id de pagamento." });

        var dataIdForSignature = MercadoPagoNotificationParser.NormalizeDataIdForWebhookSignature(dataId);

        if (!MercadoPagoWebhookSignature.TryGetTs(xSigStr, out var ts))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "ts ausente em x-signature." });

        if (!MercadoPagoWebhookSignature.IsValid(opt.WebhookSecret, xSigStr, xReqStr, dataIdForSignature, ts))
            return Unauthorized(new { code = "WEBHOOK_SIGNATURE_INVALID", message = "Assinatura inválida." });

        if (string.IsNullOrWhiteSpace(opt.AccessToken))
            return StatusCode(500, new { code = "WEBHOOK_MISCONFIGURED", message = "MERCADOPAGO_ACCESS_TOKEN ausente." });

        var client = httpClientFactory.CreateClient(nameof(MercadoPagoPaymentServiceProvider));
        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/{dataIdForSignature}");
        using var res = await client.SendAsync(get, ct);
        var paymentJson = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return BadRequest(new { code = "PSP_ERROR", message = "Falha ao consultar pagamento no Mercado Pago." });

        if (!MercadoPagoNotificationParser.TryParseApprovedPayment(paymentJson, out var paymentId, out var mpAmount,
                out var method))
            return Ok(new { ok = true, ignored = true });

        var row = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (row == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });
        if (Math.Abs(row.Amount - mpAmount) > 0.02m)
            return Conflict(new { code = "AMOUNT_MISMATCH", message = "Valor divergente do PSP." });

        var txId = $"mp:{dataIdForSignature}";
        var result = await settlement.TryMarkPaidAsync(parkingId, paymentId, txId, method, ct);
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
