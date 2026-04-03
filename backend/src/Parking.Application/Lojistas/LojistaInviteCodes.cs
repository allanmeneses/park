using System.Security.Cryptography;
using System.Text;

namespace Parking.Application.Lojistas;

/// <summary>Geração de códigos e hash do código de ativação (norma § convites lojista).</summary>
public static class LojistaInviteCodes
{
    /// <summary>Sem I, O, 0, 1 para reduzir confusão.</summary>
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string GenerateMerchantCode() => RandomFromAlphabet(10);

    public static string GenerateActivationCode() => RandomFromAlphabet(12);

    public static string HashActivationCode(string activationCode)
    {
        var trimmed = activationCode.Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trimmed))).ToLowerInvariant();
    }

    public static bool TimingSafeEqualsHash(string activationCode, string storedHexHash)
    {
        var computed = HashActivationCode(activationCode);
        var a = Encoding.UTF8.GetBytes(computed);
        var b = Encoding.UTF8.GetBytes(storedHexHash.ToLowerInvariant());
        if (a.Length != b.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string RandomFromAlphabet(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
