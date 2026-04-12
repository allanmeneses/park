using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Parking.Domain;
using Parking.Infrastructure.Audit;
using Parking.Api.Parking;
using Parking.Infrastructure.Persistence.Tenant;
using Parking.Infrastructure.Payments;
using Parking.Infrastructure.Payments.MercadoPago;

namespace Parking.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)},{nameof(UserRole.CLIENT)},{nameof(UserRole.LOJISTA)}")]
public sealed class PaymentsController(
    TenantDbContext db,
    AuditService audit,
    IPaymentServiceProvider paymentProvider,
    PaymentWebhookSettlement webhookSettlement,
    IConfiguration configuration,
    IHttpContextAccessor http,
    IMemoryCache memoryCache) : ControllerBase
{
    private Guid ParkingId => (Guid)http.HttpContext!.Items[ParkingConstants.ParkingIdItem]!;

    /// <summary>
    /// Consulta o Mercado Pago e sincroniza o estado local — usado pelo GET (polling do browser) quando o webhook falha.
    /// </summary>
    private async Task TryMercadoPagoPullAndSyncAsync(
        PaymentRow payment,
        string mpTransactionId,
        CancellationToken ct)
    {
        var json = await paymentProvider.FetchProviderPaymentJsonAsync(mpTransactionId, ct);
        if (string.IsNullOrEmpty(json))
            return;

        if (!MercadoPagoNotificationParser.TryParsePaymentSnapshot(json, out var snapshot))
            return;

        if (snapshot.ParkingPaymentId != payment.Id || Math.Abs(payment.Amount - snapshot.Amount) > 0.02m)
            return;

        if (string.Equals(snapshot.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            var txId = $"mp:{mpTransactionId}";
            _ = await webhookSettlement.TryMarkPaidAsync(ParkingId, payment.Id, txId, snapshot.Method, ct,
                allowRecoverFromExpiredOrFailed: true);
            return;
        }

        if (!MercadoPagoNotificationParser.IsRetryableMercadoPagoPaymentState(snapshot.Status))
            await MarkPaymentProviderFailureAsync(payment.Id, snapshot.Status, snapshot.StatusDetail, ct);
    }

    private async Task<IActionResult?> ValidatePackagePaymentOwnershipAsync(
        PaymentRow payment,
        string? role,
        string? entityId,
        CancellationToken ct)
    {
        if (role is not (nameof(UserRole.CLIENT) or nameof(UserRole.LOJISTA)))
            return null;

        if (payment.PackageOrderId is null)
            return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });

        var order = await db.PackageOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == payment.PackageOrderId, ct);
        if (order == null)
            return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
        if (role == nameof(UserRole.CLIENT) && order.ClientId?.ToString() != entityId)
            return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
        if (role == nameof(UserRole.LOJISTA) && order.LojistaId?.ToString() != entityId)
            return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });

        return null;
    }

    private async Task<bool> EnsurePackageOrderAwaitingPaymentAsync(Guid? packageOrderId, CancellationToken ct)
    {
        if (packageOrderId is not { } poId)
            return true;

        var ord = await db.PackageOrders.FirstOrDefaultAsync(o => o.Id == poId, ct);
        return ord is not null && ord.Status == "AWAITING_PAYMENT";
    }

    private async Task MarkPaymentProviderFailureAsync(Guid paymentId, string status, string? statusDetail, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (row == null || row.Status == PaymentStatus.PAID)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        row.Status = string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase)
            ? PaymentStatus.EXPIRED
            : PaymentStatus.FAILED;
        row.FailedReason = string.IsNullOrWhiteSpace(statusDetail) ? status : $"{status}: {statusDetail}";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task MarkPaymentPaidAsync(PaymentRow payment, PaymentMethod method, CancellationToken ct)
    {
        var fromStatus = payment.Status.ToString();
        payment.Status = PaymentStatus.PAID;
        payment.PaidAt = DateTimeOffset.UtcNow;
        payment.Method = method;
        payment.FailedReason = null;
        if (payment.TicketId is { } tid)
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == tid, ct);
            t.Status = TicketStatus.CLOSED;
        }

        if (payment.PackageOrderId is { } oid)
            await CompletePackageOrderAsync(oid, ct);

        await db.SaveChangesAsync(ct);
        await audit.AppendAsync(ParkingId, "payment", payment.Id, "PAYMENT", new { payment_id = payment.Id, from_status = fromStatus, to_status = nameof(PaymentStatus.PAID) }, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var entityId = User.FindFirst("entity_id")?.Value;
        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        var forbidden = await ValidatePackagePaymentOwnershipAsync(p, role, entityId, ct);
        if (forbidden is not null)
            return forbidden;

        // Polling do front: sem webhook, o estado local nunca vira PAID. Consulta MP aqui (throttle por pagamento).
        if (p.Status is PaymentStatus.PENDING or PaymentStatus.EXPIRED &&
            string.Equals(paymentProvider.ProviderId, "mercadopago", StringComparison.OrdinalIgnoreCase))
        {
            var probePix = await db.PixTransactions.AsNoTracking()
                .Where(x => x.PaymentId == id && x.TransactionId != null && x.TransactionId != "")
                .OrderByDescending(x => x.Active)
                .ThenByDescending(x => x.ExpiresAt)
                .FirstOrDefaultAsync(ct);
            if (probePix?.TransactionId is { } mpId)
            {
                var probeKey = $"mp.probe:{ParkingId:D}:{id:D}";
                await memoryCache.GetOrCreateAsync(probeKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(12);
                    await TryMercadoPagoPullAndSyncAsync(p, mpId, ct);
                    return 1;
                });
                p = await db.Payments.AsNoTracking().FirstAsync(x => x.Id == id, ct);
            }
            else if (!string.IsNullOrWhiteSpace(p.TransactionId))
            {
                var probeKey = $"mp.probe:{ParkingId:D}:{id:D}";
                await memoryCache.GetOrCreateAsync(probeKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(12);
                    await TryMercadoPagoPullAndSyncAsync(p, p.TransactionId, ct);
                    return 1;
                });
                p = await db.Payments.AsNoTracking().FirstAsync(x => x.Id == id, ct);
            }
        }

        var pixRow = await db.PixTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == id && x.Active, ct);
        object? pixObj = null;
        if (pixRow != null)
            pixObj = new { expires_at = pixRow.ExpiresAt, active = true };

        return Ok(new
        {
            id = p.Id,
            status = p.Status.ToString(),
            method = p.Method?.ToString(),
            amount = MoneyFormatting.Format(p.Amount),
            ticket_id = p.TicketId,
            package_order_id = p.PackageOrderId,
            paid_at = p.PaidAt,
            created_at = p.CreatedAt,
            failed_reason = p.FailedReason,
            pix = pixObj
        });
    }

    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)},{nameof(UserRole.CLIENT)},{nameof(UserRole.LOJISTA)}")]
    [HttpPost("pix")]
    public async Task<IActionResult> Pix([FromBody] PixRequest body, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var entityId = User.FindFirst("entity_id")?.Value;
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == body.PaymentId, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        if (role is nameof(UserRole.CLIENT) or nameof(UserRole.LOJISTA))
        {
            if (p.PackageOrderId is null)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            }

            var order = await db.PackageOrders.FirstOrDefaultAsync(o => o.Id == p.PackageOrderId, ct);
            if (order == null)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            }

            if (role == nameof(UserRole.CLIENT) && order.ClientId?.ToString() != entityId)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            }

            if (role == nameof(UserRole.LOJISTA) && order.LojistaId?.ToString() != entityId)
            {
                await tx.RollbackAsync(ct);
                return StatusCode(403, new { code = "FORBIDDEN", message = "Proibido." });
            }
        }

        if (p.Status == PaymentStatus.PAID)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "PAYMENT_ALREADY_PAID", message = "Já pago." });
        }

        if (p.Status == PaymentStatus.EXPIRED)
        {
            p.Status = PaymentStatus.PENDING;
            p.FailedReason = null;
        }

        if (p.PackageOrderId is { } poId)
        {
            var ord = await db.PackageOrders.FirstOrDefaultAsync(o => o.Id == poId, ct);
            if (ord == null || ord.Status != "AWAITING_PAYMENT")
            {
                await tx.RollbackAsync(ct);
                return Conflict(new { code = "INVALID_TICKET_STATE", message = "Pedido inválido." });
            }
        }

        p.Method = PaymentMethod.PIX;
        // Padrão 1200s (20 min): 300s expira o QR cedo demais e o job local marca EXPIRED antes do cliente pagar.
        var ttl = int.TryParse(configuration["PIX_DEFAULT_TTL_SECONDS"], out var s) ? s : 1200;
        var existing = await db.PixTransactions.Where(x => x.PaymentId == p.Id && x.Active).ToListAsync(ct);
        var active = existing.FirstOrDefault(x => x.ExpiresAt > DateTimeOffset.UtcNow);
        if (active != null)
        {
            await tx.CommitAsync(ct);
            return Ok(new { payment_id = p.Id, qr_code = active.QrCode, expires_at = active.ExpiresAt });
        }

        foreach (var x in existing.Where(x => x.ExpiresAt <= DateTimeOffset.UtcNow))
            x.Active = false;

        PixChargeResult charge;
        try
        {
            charge = await paymentProvider.CreatePixChargeAsync(p.Id, p.Amount, ttl, ct);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            var detail = ex.Message.Length > 800 ? ex.Message.AsSpan(0, 800).ToString() : ex.Message;
            return StatusCode(502, new { code = "PSP_ERROR", message = "Falha ao criar Pix no Mercado Pago.", detail });
        }

        var pixRow = new PixTransactionRow
        {
            Id = Guid.NewGuid(),
            PaymentId = p.Id,
            ProviderStatus = "CREATED",
            QrCode = charge.QrCode,
            ExpiresAt = charge.ExpiresAt,
            TransactionId = charge.ProviderTransactionId,
            Active = true
        };
        db.PixTransactions.Add(pixRow);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { payment_id = p.Id, qr_code = charge.QrCode, expires_at = charge.ExpiresAt });
    }

    /// <summary>
    /// Consulta o PSP (Mercado Pago) e liquida o pagamento se já estiver <c>approved</c> — atalho quando o webhook falhou ou atrasou.
    /// </summary>
    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("{id:guid}/sync-psp")]
    public async Task<IActionResult> SyncPsp(Guid id, CancellationToken ct)
    {
        if (!string.Equals(paymentProvider.ProviderId, "mercadopago", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                code = "PSP_SYNC_UNSUPPORTED",
                message = "Sincronização manual só está disponível com Mercado Pago.",
            });
        }

        var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        if (p.Status == PaymentStatus.PAID)
            return Ok(new { synced = true, status = nameof(PaymentStatus.PAID) });

        var pixRow = await db.PixTransactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PaymentId == id && x.Active, ct);
        if (pixRow == null || string.IsNullOrWhiteSpace(pixRow.TransactionId))
        {
            return Conflict(new
            {
                code = "NO_ACTIVE_PIX",
                message = "Não há cobrança PIX ativa para este pagamento. Gere um novo QR.",
            });
        }

        var json = await paymentProvider.FetchProviderPaymentJsonAsync(pixRow.TransactionId, ct);
        if (string.IsNullOrEmpty(json))
        {
            return StatusCode(502, new
            {
                code = "PSP_ERROR",
                message = "Não foi possível consultar o pagamento no Mercado Pago.",
            });
        }

        if (!MercadoPagoNotificationParser.TryParseApprovedPayment(json, out var extPid, out var mpAmount, out var method))
        {
            var st = MercadoPagoNotificationParser.GetMercadoPagoApiPaymentStatus(json);
            return Ok(new { synced = false, psp_status = st });
        }

        if (extPid != p.Id)
        {
            return Conflict(new
            {
                code = "PAYMENT_MISMATCH",
                message = "Referência do PSP não corresponde a este pagamento.",
            });
        }

        if (Math.Abs(p.Amount - mpAmount) > 0.02m)
        {
            return Conflict(new { code = "AMOUNT_MISMATCH", message = "Valor divergente do PSP." });
        }

        var txId = $"mp:{pixRow.TransactionId}";
        var result = await webhookSettlement.TryMarkPaidAsync(ParkingId, p.Id, txId, method, ct,
            allowRecoverFromExpiredOrFailed: true);
        return result switch
        {
            PaymentWebhookSettlementStatus.Ok => Ok(new { synced = true, status = nameof(PaymentStatus.PAID) }),
            PaymentWebhookSettlementStatus.Duplicate => Ok(new { synced = true, status = nameof(PaymentStatus.PAID), duplicate = true }),
            PaymentWebhookSettlementStatus.IgnoredAlreadyPaid => Ok(new { synced = true, status = nameof(PaymentStatus.PAID), ignored = true }),
            PaymentWebhookSettlementStatus.NotFound => NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." }),
            PaymentWebhookSettlementStatus.Late => Conflict(new { code = "WEBHOOK_LATE", message = "Pagamento expirado no sistema." }),
            PaymentWebhookSettlementStatus.InvalidState => Conflict(new { code = "INVALID_PAYMENT_STATE", message = "Estado inválido." }),
            _ => StatusCode(500)
        };
    }

    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)},{nameof(UserRole.CLIENT)},{nameof(UserRole.LOJISTA)}")]
    [HttpPost("card")]
    public async Task<IActionResult> Card([FromBody] CardRequest body, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var entityId = User.FindFirst("entity_id")?.Value;
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == body.PaymentId, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        var forbidden = await ValidatePackagePaymentOwnershipAsync(p, role, entityId, ct);
        if (forbidden is not null)
        {
            await tx.RollbackAsync(ct);
            return forbidden;
        }

        if (p.Amount != body.Amount)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "AMOUNT_MISMATCH", message = "Valor divergente." });
        }

        if (p.Status == PaymentStatus.PAID)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "PAYMENT_ALREADY_PAID", message = "Já pago." });
        }

        if (!await EnsurePackageOrderAwaitingPaymentAsync(p.PackageOrderId, ct))
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "INVALID_TICKET_STATE", message = "Pedido inválido." });
        }

        var requestedFlow = (body.Flow ?? "").Trim().ToUpperInvariant();
        if (requestedFlow == "EMBEDDED")
        {
            if (!paymentProvider.SupportsEmbeddedCardPayments)
            {
                await tx.RollbackAsync(ct);
                return Conflict(new { code = "CARD_EMBEDDED_UNSUPPORTED", message = "Cartão embutido indisponível no PSP atual." });
            }

            if (p.Status is PaymentStatus.EXPIRED or PaymentStatus.FAILED)
            {
                p.Status = PaymentStatus.PENDING;
                p.FailedReason = null;
            }

            p.Method = PaymentMethod.CARD;
            await db.SaveChangesAsync(ct);

            if (string.IsNullOrWhiteSpace(body.Token))
            {
                EmbeddedCardSession session;
                try
                {
                    session = await paymentProvider.CreateEmbeddedCardSessionAsync(p.Id, p.Amount, ct);
                }
                catch (InvalidOperationException ex)
                {
                    await tx.RollbackAsync(ct);
                    var detail = ex.Message.Length > 800 ? ex.Message.AsSpan(0, 800).ToString() : ex.Message;
                    return StatusCode(502, new { code = "PSP_ERROR", message = "Falha ao preparar cartão embutido no Mercado Pago.", detail });
                }

                await tx.CommitAsync(ct);
                return Ok(new
                {
                    payment_id = p.Id,
                    mode = "embedded_bricks",
                    provider = paymentProvider.ProviderId,
                    public_key = session.PublicKey,
                    amount = MoneyFormatting.Format(p.Amount)
                });
            }

            if (body.Installments is null or < 1 || string.IsNullOrWhiteSpace(body.PaymentMethodId) || string.IsNullOrWhiteSpace(body.PayerEmail))
            {
                await tx.RollbackAsync(ct);
                return BadRequest(new { code = "VALIDATION_ERROR", message = "Dados do cartão incompletos." });
            }

            EmbeddedCardPaymentResult result;
            try
            {
                result = await paymentProvider.SubmitEmbeddedCardPaymentAsync(new EmbeddedCardPaymentRequest(
                    p.Id,
                    p.Amount,
                    body.Token.Trim(),
                    body.Installments.Value,
                    body.PaymentMethodId.Trim(),
                    string.IsNullOrWhiteSpace(body.IssuerId) ? null : body.IssuerId.Trim(),
                    body.PayerEmail.Trim(),
                    string.IsNullOrWhiteSpace(body.IdentificationType) ? null : body.IdentificationType.Trim(),
                    string.IsNullOrWhiteSpace(body.IdentificationNumber) ? null : body.IdentificationNumber.Trim()), ct);
            }
            catch (InvalidOperationException ex)
            {
                await tx.RollbackAsync(ct);
                var detail = ex.Message.Length > 800 ? ex.Message.AsSpan(0, 800).ToString() : ex.Message;
                return StatusCode(502, new { code = "PSP_ERROR", message = "Falha ao processar cartão no Mercado Pago.", detail });
            }

            p.TransactionId = result.ProviderTransactionId;
            p.FailedReason = null;

            if (string.Equals(result.ProviderStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                await MarkPaymentPaidAsync(p, PaymentMethod.CARD, ct);
                await tx.CommitAsync(ct);
                return Ok(new
                {
                    payment_id = p.Id,
                    status = nameof(PaymentStatus.PAID),
                    provider = paymentProvider.ProviderId,
                    provider_status = result.ProviderStatus,
                    provider_status_detail = result.ProviderStatusDetail
                });
            }

            if (MercadoPagoNotificationParser.IsRetryableMercadoPagoPaymentState(result.ProviderStatus))
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Ok(new
                {
                    payment_id = p.Id,
                    status = nameof(PaymentStatus.PENDING),
                    provider = paymentProvider.ProviderId,
                    provider_status = result.ProviderStatus,
                    provider_status_detail = result.ProviderStatusDetail
                });
            }

            p.Status = string.Equals(result.ProviderStatus, "cancelled", StringComparison.OrdinalIgnoreCase)
                ? PaymentStatus.EXPIRED
                : PaymentStatus.FAILED;
            p.FailedReason = string.IsNullOrWhiteSpace(result.ProviderStatusDetail)
                ? result.ProviderStatus
                : $"{result.ProviderStatus}: {result.ProviderStatusDetail}";
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Ok(new
            {
                payment_id = p.Id,
                status = p.Status.ToString(),
                provider = paymentProvider.ProviderId,
                provider_status = result.ProviderStatus,
                provider_status_detail = result.ProviderStatusDetail
            });
        }

        if (paymentProvider.CardFlow == CardPaymentFlow.HostedCheckout)
        {
            if (p.Status is PaymentStatus.EXPIRED or PaymentStatus.FAILED)
            {
                p.Status = PaymentStatus.PENDING;
                p.FailedReason = null;
            }

            p.Method = PaymentMethod.CARD;
            await db.SaveChangesAsync(ct);

            CardCheckoutSession session;
            try
            {
                session = await paymentProvider.CreateCardCheckoutAsync(p.Id, p.Amount, ct);
            }
            catch (InvalidOperationException ex)
            {
                await tx.RollbackAsync(ct);
                var detail = ex.Message.Length > 800 ? ex.Message.AsSpan(0, 800).ToString() : ex.Message;
                return StatusCode(502, new { code = "PSP_ERROR", message = "Falha ao criar checkout no Mercado Pago.", detail });
            }

            await tx.CommitAsync(ct);
            return Ok(new
            {
                payment_id = p.Id,
                mode = "hosted_checkout",
                provider = paymentProvider.ProviderId,
                preference_id = session.PreferenceId,
                init_point = session.InitPointUrl,
                sandbox_init_point = session.SandboxInitPointUrl,
                public_key = session.PublicKey
            });
        }

        await MarkPaymentPaidAsync(p, PaymentMethod.CARD, ct);
        await tx.CommitAsync(ct);
        return Ok(new
        {
            payment_id = p.Id,
            status = nameof(PaymentStatus.PAID),
            provider = paymentProvider.ProviderId
        });
    }

    [Authorize(Roles = $"{nameof(UserRole.OPERATOR)},{nameof(UserRole.MANAGER)},{nameof(UserRole.ADMIN)},{nameof(UserRole.SUPER_ADMIN)}")]
    [HttpPost("cash")]
    public async Task<IActionResult> Cash([FromBody] CashPayRequest body, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var open = await db.CashSessions.FirstOrDefaultAsync(s => s.Status == CashSessionStatus.OPEN, ct);
        if (open == null)
        {
            await tx.RollbackAsync(ct);
            return Conflict(new { code = "CASH_SESSION_REQUIRED", message = "Caixa fechado." });
        }

        var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == body.PaymentId, ct);
        if (p == null)
            return NotFound(new { code = "NOT_FOUND", message = "Pagamento não encontrado." });

        p.Method = PaymentMethod.CASH;
        p.Status = PaymentStatus.PAID;
        p.PaidAt = DateTimeOffset.UtcNow;
        open.ExpectedAmount += p.Amount;
        if (p.TicketId is { } tid)
        {
            var t = await db.Tickets.FirstAsync(x => x.Id == tid, ct);
            t.Status = TicketStatus.CLOSED;
        }

        if (p.PackageOrderId is { } oid)
            await CompletePackageOrderAsync(oid, ct);

        await db.SaveChangesAsync(ct);
        await audit.AppendAsync(ParkingId, "payment", p.Id, "PAYMENT", new { payment_id = p.Id, from_status = nameof(PaymentStatus.PENDING), to_status = nameof(PaymentStatus.PAID) }, ct);
        await tx.CommitAsync(ct);
        return Ok(new { payment_id = p.Id, status = nameof(PaymentStatus.PAID) });
    }

    private async Task CompletePackageOrderAsync(Guid orderId, CancellationToken ct)
    {
        var ord = await db.PackageOrders.FirstAsync(o => o.Id == orderId, ct);
        ord.Status = "PAID";
        ord.PaidAt = DateTimeOffset.UtcNow;
        var pkg = await db.RechargePackages.FirstAsync(p => p.Id == ord.PackageId, ct);
        if (ord.Scope == "CLIENT" && ord.ClientId is { } cid)
        {
            var w = await db.ClientWallets.FirstOrDefaultAsync(x => x.ClientId == cid, ct);
            if (w == null)
            {
                w = new ClientWalletRow { Id = Guid.NewGuid(), ClientId = cid, BalanceHours = 0, ExpirationDate = null };
                db.ClientWallets.Add(w);
            }

            w.BalanceHours += pkg.Hours;
            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = cid,
                LojistaId = null,
                DeltaHours = pkg.Hours,
                Amount = ord.Amount,
                PackageId = pkg.Id,
                Settlement = ord.Settlement,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else if (ord.Scope == "LOJISTA" && ord.LojistaId is { } lid)
        {
            var w = await db.LojistaWallets.FirstOrDefaultAsync(x => x.LojistaId == lid, ct);
            if (w == null)
            {
                w = new LojistaWalletRow { Id = Guid.NewGuid(), LojistaId = lid, BalanceHours = 0 };
                db.LojistaWallets.Add(w);
            }

            w.BalanceHours += pkg.Hours;
            db.WalletLedger.Add(new WalletLedgerRow
            {
                Id = Guid.NewGuid(),
                ClientId = null,
                LojistaId = lid,
                DeltaHours = pkg.Hours,
                Amount = ord.Amount,
                PackageId = pkg.Id,
                Settlement = ord.Settlement,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await audit.AppendAsync(ParkingId, "package", orderId, "PACKAGE_PURCHASE", new { order_id = orderId, package_id = pkg.Id, settlement = ord.Settlement }, ct);
    }

    public sealed record PixRequest([property: System.Text.Json.Serialization.JsonPropertyName("payment_id")] Guid PaymentId);
    public sealed record CardRequest(
        Guid PaymentId,
        decimal Amount,
        string? Flow = null,
        string? Token = null,
        int? Installments = null,
        string? PaymentMethodId = null,
        string? IssuerId = null,
        string? PayerEmail = null,
        string? IdentificationType = null,
        string? IdentificationNumber = null);
    public sealed record CashPayRequest(Guid PaymentId);
}
