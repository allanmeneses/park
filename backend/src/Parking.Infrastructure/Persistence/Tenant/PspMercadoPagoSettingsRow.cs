using System.ComponentModel.DataAnnotations.Schema;

namespace Parking.Infrastructure.Persistence.Tenant;

/// <summary>Configuração Mercado Pago por tenant (credenciais do dono do estacionamento).</summary>
[Table("psp_mercado_pago_settings")]
public sealed class PspMercadoPagoSettingsRow
{
    public Guid Id { get; set; }

    /// <summary>Quando false, usam-se as variáveis globais MERCADOPAGO_* (comportamento legado).</summary>
    public bool UseTenantCredentials { get; set; }

    /// <summary>SANDBOX ou PRODUCTION (informativo; o token de teste/produção isola no MP).</summary>
    public string Environment { get; set; } = "PRODUCTION";

    public string AccessTokenCipher { get; set; } = "";

    public string WebhookSecretCipher { get; set; } = "";

    public string PublicKey { get; set; } = "";

    public string PayerEmail { get; set; } = "";

    public string? ApiBaseUrl { get; set; }

    public string? CheckoutBackSuccessUrl { get; set; }

    public string? CheckoutBackFailureUrl { get; set; }

    public string? CheckoutBackPendingUrl { get; set; }

    public DateTimeOffset? CredentialsAcknowledgedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
