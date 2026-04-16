using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parking.Api.Parking;
using Parking.Domain;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Payments;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/settings/psp/mercadopago")]
[Authorize(Roles = $"{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
public sealed class PspMercadoPagoSettingsController(
    TenantDbContext db,
    AuditService audit,
    IdentityDbContext identity,
    TenantSecretProtector protector,
    IHttpContextAccessor http) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await PspMercadoPagoSettingsGuard.EnsureAsync(db, ct);
        var row = await db.PspMercadoPagoSettings.AsNoTracking()
            .FirstAsync(x => x.Id == PspMercadoPagoSettingsGuard.SingletonId, ct);

        return Ok(new
        {
            use_tenant_credentials = row.UseTenantCredentials,
            environment = row.Environment,
            public_key = row.PublicKey,
            payer_email = row.PayerEmail,
            has_access_token = !string.IsNullOrWhiteSpace(row.AccessTokenCipher),
            has_webhook_secret = !string.IsNullOrWhiteSpace(row.WebhookSecretCipher),
            api_base_url = row.ApiBaseUrl,
            checkout_back_success_url = row.CheckoutBackSuccessUrl,
            checkout_back_failure_url = row.CheckoutBackFailureUrl,
            checkout_back_pending_url = row.CheckoutBackPendingUrl,
            credentials_acknowledged_at = row.CredentialsAcknowledgedAt,
            updated_at = row.UpdatedAt,
        });
    }

    [HttpPut]
    [Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    public async Task<IActionResult> Put([FromBody] PspMercadoPagoPut body, CancellationToken ct)
    {
        var actorRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        if (actorRole == nameof(UserRole.SUPER_ADMIN) && string.IsNullOrWhiteSpace(body.SupportReason))
        {
            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "SUPER_ADMIN deve enviar support_reason ao alterar a configuração PSP.",
            });
        }

        if (body.UseTenantCredentials && !body.Acknowledged)
        {
            return BadRequest(new
            {
                code = "VALIDATION_ERROR",
                message = "Confirme acknowledged=true (responsabilidade pelas credenciais da conta Mercado Pago).",
            });
        }

        if (body.UseTenantCredentials)
        {
            if (string.IsNullOrWhiteSpace(body.AccessToken) ||
                string.IsNullOrWhiteSpace(body.WebhookSecret) ||
                string.IsNullOrWhiteSpace(body.PublicKey) ||
                string.IsNullOrWhiteSpace(body.PayerEmail))
            {
                return BadRequest(new
                {
                    code = "VALIDATION_ERROR",
                    message = "Com use_tenant_credentials=true, access_token, webhook_secret, public_key e payer_email são obrigatórios.",
                });
            }

            if (!protector.IsConfigured)
            {
                return StatusCode(503, new
                {
                    code = "PSP_ENCRYPTION_UNAVAILABLE",
                    message = "Servidor sem TENANT_SECRET_ENCRYPTION_KEY; não é possível gravar credenciais do tenant.",
                });
            }

            var env = (body.Environment ?? "").Trim().ToUpperInvariant();
            if (env is not ("SANDBOX" or "PRODUCTION"))
            {
                return BadRequest(new { code = "VALIDATION_ERROR", message = "environment deve ser SANDBOX ou PRODUCTION." });
            }
        }

        await PspMercadoPagoSettingsGuard.EnsureAsync(db, ct);
        var row = await db.PspMercadoPagoSettings.FirstAsync(x => x.Id == PspMercadoPagoSettingsGuard.SingletonId, ct);

        var oldUse = row.UseTenantCredentials;
        var oldEnv = row.Environment;
        var oldPkMask = MaskKey(row.PublicKey);
        var oldEmail = row.PayerEmail;
        var oldHasAt = !string.IsNullOrEmpty(row.AccessTokenCipher) ? "[definido]" : "[vazio]";
        var oldHasWh = !string.IsNullOrEmpty(row.WebhookSecretCipher) ? "[definido]" : "[vazio]";
        var oldApiBase = row.ApiBaseUrl ?? "";
        var oldSucc = row.CheckoutBackSuccessUrl ?? "";
        var oldFail = row.CheckoutBackFailureUrl ?? "";
        var oldPend = row.CheckoutBackPendingUrl ?? "";

        var changes = new List<PspAuditChange>();

        void Track(string field, string label, string from, string to)
        {
            if (from != to)
                changes.Add(new PspAuditChange(field, label, from, to));
        }

        row.UseTenantCredentials = body.UseTenantCredentials;
        if (body.UseTenantCredentials)
        {
            row.Environment = (body.Environment ?? "PRODUCTION").Trim().ToUpperInvariant();
            row.AccessTokenCipher = protector.Protect(body.AccessToken!.Trim());
            row.WebhookSecretCipher = protector.Protect(body.WebhookSecret!.Trim());
            row.PublicKey = body.PublicKey!.Trim();
            row.PayerEmail = body.PayerEmail!.Trim();
            row.ApiBaseUrl = string.IsNullOrWhiteSpace(body.ApiBaseUrl) ? null : body.ApiBaseUrl.Trim();
            row.CheckoutBackSuccessUrl = string.IsNullOrWhiteSpace(body.CheckoutBackSuccessUrl) ? null : body.CheckoutBackSuccessUrl.Trim();
            row.CheckoutBackFailureUrl = string.IsNullOrWhiteSpace(body.CheckoutBackFailureUrl) ? null : body.CheckoutBackFailureUrl.Trim();
            row.CheckoutBackPendingUrl = string.IsNullOrWhiteSpace(body.CheckoutBackPendingUrl) ? null : body.CheckoutBackPendingUrl.Trim();
            row.CredentialsAcknowledgedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            row.Environment = "PRODUCTION";
            row.AccessTokenCipher = "";
            row.WebhookSecretCipher = "";
            row.PublicKey = "";
            row.PayerEmail = "";
            row.ApiBaseUrl = null;
            row.CheckoutBackSuccessUrl = null;
            row.CheckoutBackFailureUrl = null;
            row.CheckoutBackPendingUrl = null;
            row.CredentialsAcknowledgedAt = null;
        }

        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedByUserId = ParseActorUserId();

        Track("use_tenant_credentials", "Usar credenciais do tenant", oldUse.ToString().ToLowerInvariant(),
            row.UseTenantCredentials.ToString().ToLowerInvariant());
        Track("environment", "Ambiente MP", oldEnv, row.Environment);
        Track("public_key", "Chave pública", oldPkMask, MaskKey(row.PublicKey));
        Track("payer_email", "E-mail pagador", oldEmail, row.PayerEmail);
        Track("access_token", "Access token", oldHasAt, string.IsNullOrEmpty(row.AccessTokenCipher) ? "[vazio]" : "[definido]");
        Track("webhook_secret", "Segredo webhook", oldHasWh, string.IsNullOrEmpty(row.WebhookSecretCipher) ? "[vazio]" : "[definido]");
        Track("api_base_url", "URL base API", oldApiBase, row.ApiBaseUrl ?? "");
        Track("checkout_back_success_url", "URL volta sucesso", oldSucc, row.CheckoutBackSuccessUrl ?? "");
        Track("checkout_back_failure_url", "URL volta falha", oldFail, row.CheckoutBackFailureUrl ?? "");
        Track("checkout_back_pending_url", "URL volta pendente", oldPend, row.CheckoutBackPendingUrl ?? "");

        await db.SaveChangesAsync(ct);

        if (changes.Count > 0)
        {
            var actorUserId = ParseActorUserId();
            var actorEmail = actorUserId is { } uid
                ? await identity.Users.AsNoTracking().Where(x => x.Id == uid).Select(x => x.Email).FirstOrDefaultAsync(ct)
                : null;
            await audit.AppendAsync(
                ParkingId,
                "psp_mercado_pago",
                PspMercadoPagoSettingsGuard.SingletonId,
                "PSP_MERCADOPAGO_UPDATE",
                new PspAuditPayload(actorUserId, actorEmail, actorRole, body.SupportReason, changes),
                ct);
        }

        return Ok(new { ok = true });
    }

    private Guid? ParseActorUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var actorUserId) ? actorUserId : null;
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";
        return key.Length <= 8 ? "****" : key[..4] + "…" + key[^4..];
    }

    public sealed record PspMercadoPagoPut(
        bool UseTenantCredentials,
        bool Acknowledged,
        string? Environment,
        string? AccessToken,
        string? WebhookSecret,
        string? PublicKey,
        string? PayerEmail,
        string? ApiBaseUrl,
        string? CheckoutBackSuccessUrl,
        string? CheckoutBackFailureUrl,
        string? CheckoutBackPendingUrl,
        string? SupportReason);

    private sealed record PspAuditChange(string Field, string Label, string From, string To);

    private sealed record PspAuditPayload(
        Guid? ActorUserId,
        string? ActorEmail,
        string? ActorRole,
        string? SupportReason,
        IReadOnlyList<PspAuditChange> Changes);
}
