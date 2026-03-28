using System.Text.RegularExpressions;

namespace Parking.Application.Validation;

public static class PlateValidator
{
    private static readonly Regex Mercosul = new(
        "^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$",
        RegexOptions.Compiled);

    private static readonly Regex Legado = new(
        "^[A-Z]{3}[0-9]{4}$",
        RegexOptions.Compiled);

    /// <summary>Normaliza: maiúsculas, remove espaços e hífens.</summary>
    public static string Normalize(string plate)
    {
        var s = plate.ToUpperInvariant().Replace(" ", "").Replace("-", "");
        return s;
    }

    public static bool IsValidNormalized(string normalizedPlate) =>
        Mercosul.IsMatch(normalizedPlate) || Legado.IsMatch(normalizedPlate);
}
