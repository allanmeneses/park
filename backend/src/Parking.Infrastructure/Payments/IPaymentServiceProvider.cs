namespace Parking.Infrastructure.Payments;

/// <summary>Fluxo de cartão suportado pelo PSP. <see cref="CardPaymentFlow.InPersonSimulated"/> = confirmação síncrona (stub/desenvolvimento).</summary>
public enum CardPaymentFlow
{
    InPersonSimulated,
    HostedCheckout
}

/// <summary>Resultado de criação de cobrança Pix (QR copia-e-cola / EMV).</summary>
public sealed record PixChargeResult(string QrCode, DateTimeOffset ExpiresAt, string? ProviderTransactionId);

/// <summary>Sessão de checkout hospedado no PSP (ex.: Preference do Mercado Pago para crédito/débito online).</summary>
public sealed record CardCheckoutSession(
    string PreferenceId,
    string InitPointUrl,
    string? SandboxInitPointUrl,
    string? PublicKey);

/// <summary>
/// Provedor de pagamento intercambiável (Pix + cartão). Novas implementações: Efí, Stone/Pagar.me, etc.
/// </summary>
public interface IPaymentServiceProvider
{
    /// <summary>Identificador estável para telemetria e respostas API (ex.: stub, mercadopago).</summary>
    string ProviderId { get; }

    CardPaymentFlow CardFlow { get; }

    Task<PixChargeResult> CreatePixChargeAsync(Guid paymentId, decimal amount, int expiresInSeconds, CancellationToken ct);

    /// <summary>
    /// Corpo JSON bruto de GET no PSP para o id da transação (ex. <c>/v1/payments/{id}</c> no Mercado Pago).
    /// <c>null</c> se o PSP não suporta consulta ou a chamada falhou.
    /// </summary>
    Task<string?> FetchProviderPaymentJsonAsync(string providerPaymentId, CancellationToken ct);

    /// <summary>
    /// Cria intenção de pagamento com valor fixo no PSP (operador não define valor).
    /// Só aplicável quando <see cref="CardFlow"/> é <see cref="CardPaymentFlow.HostedCheckout"/>.
    /// </summary>
    Task<CardCheckoutSession> CreateCardCheckoutAsync(Guid paymentId, decimal amount, CancellationToken ct);
}
