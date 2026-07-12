using Fenrir.Application.Game.Domain.Skills;

namespace Fenrir.Application.Game.Tests.Skills;

public class SupportSkillTimeUpRatioCalculatorTests
{
    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Compute_ReturnsExpectedRatio(bool buffDurationExtensionActive, bool premiumActive, int expected)
    {
        var ratio = SupportSkillTimeUpRatioCalculator.Compute(buffDurationExtensionActive, premiumActive);

        Assert.Equal(expected, ratio);
    }
}
