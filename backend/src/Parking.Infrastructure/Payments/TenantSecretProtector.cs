using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Parking.Infrastructure.Payments;

/// <summary>Cifra valores sensíveis por tenant (AES-256-GCM). Chave em <c>TENANT_SECRET_ENCRYPTION_KEY</c> (Base64, 32 bytes).</summary>
public sealed class TenantSecretProtector
{
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private readonly byte[] _key;

    public TenantSecretProtector(IConfiguration configuration)
    {
        var b64 = configuration["TENANT_SECRET_ENCRYPTION_KEY"]?.Trim();
        if (string.IsNullOrEmpty(b64))
        {
            _key = [];
            return;
        }

        try
        {
            var k = Convert.FromBase64String(b64);
            if (k.Length != KeyLength)
                throw new InvalidOperationException("TENANT_SECRET_ENCRYPTION_KEY must be Base64 encoding of exactly 32 bytes.");
            _key = k;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("TENANT_SECRET_ENCRYPTION_KEY must be valid Base64.", ex);
        }
    }

    public bool IsConfigured => _key.Length == KeyLength;

    public string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return "";
        if (!IsConfigured)
            throw new InvalidOperationException("TENANT_SECRET_ENCRYPTION_KEY is not configured on the server.");

        var plain = Encoding.UTF8.GetBytes(plainText);
        Span<byte> nonce = stackalloc byte[NonceLength];
        RandomNumberGenerator.Fill(nonce);

        Span<byte> tag = stackalloc byte[TagLength];
        var cipher = new byte[plain.Length];
        using (var aes = new AesGcm(_key, TagLength))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }

        // layout: v1(1) | nonce | tag | cipher
        var payload = new byte[1 + NonceLength + TagLength + cipher.Length];
        payload[0] = 1;
        nonce.CopyTo(payload.AsSpan(1));
        tag.CopyTo(payload.AsSpan(1 + NonceLength));
        cipher.CopyTo(payload.AsSpan(1 + NonceLength + TagLength));
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string? cipherBase64)
    {
        if (string.IsNullOrEmpty(cipherBase64))
            return "";
        if (!IsConfigured)
            throw new InvalidOperationException("TENANT_SECRET_ENCRYPTION_KEY is not configured on the server.");

        var payload = Convert.FromBase64String(cipherBase64);
        if (payload.Length < 1 + NonceLength + TagLength + 1 || payload[0] != 1)
            throw new InvalidOperationException("Invalid cipher payload.");

        var nonce = payload.AsSpan(1, NonceLength);
        var tag = payload.AsSpan(1 + NonceLength, TagLength);
        var cipher = payload.AsSpan(1 + NonceLength + TagLength);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, TagLength))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }
}
