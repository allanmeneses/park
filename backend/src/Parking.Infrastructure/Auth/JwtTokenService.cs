using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;

namespace Parking.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "parking-identity";
    public string Audience { get; set; } = "parking-api";
    public int AccessTokenSeconds { get; set; } = 28800;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public string CreateAccessToken(ParkingIdentityUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (user.ParkingId is { } pid)
            claims.Add(new Claim("parking_id", pid.ToString()));
        if (user.EntityId is { } eid)
            claims.Add(new Claim("entity_id", eid.ToString()));

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.UtcDateTime.AddSeconds(_opt.AccessTokenSeconds),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static byte[] HashRefreshToken(string opaqueToken)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(opaqueToken));
    }

    public static string NewOpaqueRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
