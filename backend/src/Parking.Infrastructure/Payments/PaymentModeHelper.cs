using Microsoft.Extensions.Configuration;

namespace Parking.Infrastructure.Payments;

/// <summary>Alinha a escolha Stub vs Mercado Pago à configuração global (<c>PAYMENT_PSP</c> / <c>PIX_MODE</c>).</summary>
public static class PaymentModeHelper
{
    public static bool IsGlobalMercadoPago(IConfiguration configuration)
    {
        var psp = configuration["PAYMENT_PSP"]?.Trim();
        if (string.IsNullOrEmpty(psp) &&
            string.Equals(configuration["PIX_MODE"], "Production", StringComparison.OrdinalIgnoreCase))
            psp = "MercadoPago";
        if (string.IsNullOrEmpty(psp))
            psp = "Stub";
        return string.Equals(psp, "MercadoPago", StringComparison.OrdinalIgnoreCase);
    }
}
