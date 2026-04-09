namespace Parking.Infrastructure.Payments;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>Access token de produção ou teste (Bearer).</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Chave pública (ex.: APP_USR-...) para SDK mobile / Payment Brick.</summary>
    public string PublicKey { get; set; } = "";

    /// <summary>Segredo de assinatura de webhooks (painel Mercado Pago).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Base da API REST (sandbox usa o mesmo host; token de teste isola).</summary>
    public string ApiBaseUrl { get; set; } = "https://api.mercadopago.com";

    /// <summary>E-mail exigido pela API na criação de pagamento Pix; pode ser genérico do estacionamento.</summary>
    public string PayerEmail { get; set; } = "parking-payer@example.com";

    /// <summary>URLs de retorno opcionais para checkout (deep links ou web).</summary>
    public string? CheckoutBackSuccessUrl { get; set; }

    public string? CheckoutBackFailureUrl { get; set; }

    public string? CheckoutBackPendingUrl { get; set; }
}
