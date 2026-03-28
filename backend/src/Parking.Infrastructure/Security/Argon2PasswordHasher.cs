using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Konscious.Security.Cryptography;

namespace Parking.Infrastructure.Security;

/// <summary>Argon2id PHC: m=19456, t=2, p=1, salt 16 bytes (SPEC §3).</summary>
public static class Argon2PasswordHasher
{
    private const int MemoryKiB = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int SaltLen = 16;
    private const int HashLen = 32;

    private static readonly Regex Phc = new(
        @"^\$argon2id\$v=(?<ver>\d+)\$m=(?<m>\d+),t=(?<t>\d+),p=(?<p>\d+)\$(?<salt>[^$]+)\$(?<hash>[^$]+)$",
        RegexOptions.Compiled);

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLen);
        var hash = HashCore(password, salt, MemoryKiB, Iterations, Parallelism);
        var saltB64 = ToPhcB64(salt);
        var hashB64 = ToPhcB64(hash);
        return $"$argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}${saltB64}${hashB64}";
    }

    public static bool Verify(string password, string phcString)
    {
        var m = Phc.Match(phcString);
        if (!m.Success) return false;
        var mParam = int.Parse(m.Groups["m"].Value);
        var tParam = int.Parse(m.Groups["t"].Value);
        var pParam = int.Parse(m.Groups["p"].Value);
        var salt = FromPhcB64(m.Groups["salt"].Value);
        var expected = FromPhcB64(m.Groups["hash"].Value);
        if (salt.Length == 0 || expected.Length == 0) return false;
        var actual = HashCore(password, salt, mParam, tParam, pParam);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] HashCore(string password, byte[] salt, int memoryKiB, int iterations, int parallelism)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };
        return argon2.GetBytes(HashLen);
    }

    private static string ToPhcB64(byte[] data) => Convert.ToBase64String(data).TrimEnd('=');

    private static byte[] FromPhcB64(string s)
    {
        var pad = (4 - (s.Length % 4)) % 4;
        if (pad > 0) s += new string('=', pad);
        return Convert.FromBase64String(s);
    }
}
