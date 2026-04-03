using Parking.Application.Lojistas;

namespace Parking.Tests.Unit;

public sealed class LojistaInviteCodesTests
{
    [Fact]
    public void HashActivationCode_is_sha256_hex_64_chars()
    {
        var h = LojistaInviteCodes.HashActivationCode("promo-secret");
        Assert.Equal(64, h.Length);
        Assert.True(h.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public void TimingSafeEqualsHash_matches_and_rejects_wrong_or_bad_length()
    {
        var code = "ativacao12";
        var h = LojistaInviteCodes.HashActivationCode(code);
        Assert.True(LojistaInviteCodes.TimingSafeEqualsHash(code, h));
        Assert.False(LojistaInviteCodes.TimingSafeEqualsHash("other", h));
    }
}
