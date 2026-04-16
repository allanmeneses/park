using Microsoft.Extensions.Configuration;
using Parking.Infrastructure.Payments;
using Xunit;

namespace Parking.Tests.Unit;

public sealed class TenantSecretProtectorTests
{
    [Fact]
    public void Protect_roundtrip()
    {
        var key = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string('z', 32)));
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TENANT_SECRET_ENCRYPTION_KEY"] = key
        }).Build();
        var p = new TenantSecretProtector(cfg);
        const string secret = "mp-access-token-xyz";
        var c = p.Protect(secret);
        Assert.NotEqual(secret, c);
        Assert.Equal(secret, p.Unprotect(c));
    }

    [Fact]
    public void Empty_plain_returns_empty_cipher()
    {
        var key = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string('z', 32)));
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TENANT_SECRET_ENCRYPTION_KEY"] = key
        }).Build();
        var p = new TenantSecretProtector(cfg);
        Assert.Equal("", p.Protect(""));
        Assert.Equal("", p.Unprotect(""));
    }
}
