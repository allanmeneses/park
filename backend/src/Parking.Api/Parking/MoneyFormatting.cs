using System.Globalization;

namespace Parking.Api.Parking;

/// <summary>Valores monetários em JSON como string com separador decimal fixo (SPEC §1.1 — contrato estável para clientes).</summary>
public static class MoneyFormatting
{
    public static string Format(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}
