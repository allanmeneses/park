using Parking.Application.Validation;
using Xunit;

namespace Parking.Tests.Application;

public sealed class PlateValidatorTests
{
    [Theory]
    [InlineData("abc1d23", true)]
    [InlineData("ABC-1234", true)]
    [InlineData("ABCD123", false)]
    public void Normalizes_and_validates(string input, bool ok)
    {
        var n = PlateValidator.Normalize(input);
        Assert.Equal(ok, PlateValidator.IsValidNormalized(n));
    }
}
