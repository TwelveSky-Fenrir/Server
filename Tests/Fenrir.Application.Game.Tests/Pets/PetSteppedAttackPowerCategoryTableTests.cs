using Fenrir.Application.Game.Domain.Pets;

namespace Fenrir.Application.Game.Tests.Pets;

public class PetSteppedAttackPowerCategoryTableTests
{
    [Theory]
    [InlineData(1002, 40_000_000)]
    [InlineData(1004, 40_000_000)]
    [InlineData(2140, 40_000_000)]
    [InlineData(8202, 40_000_000)]
    [InlineData(1006, 80_000_000)]
    [InlineData(17052, 80_000_000)]
    [InlineData(8211, 80_000_000)]
    [InlineData(1012, 160_000_000)]
    [InlineData(17053, 160_000_000)]
    [InlineData(8212, 160_000_000)]
    [InlineData(1016, 320_000_000)]
    [InlineData(2160, 320_000_000)]
    [InlineData(17057, 320_000_000)]
    [InlineData(8216, 320_000_000)]
    public void TryResolveTierMax_RecognizedId_ResolvesCorrectCategoryMaximum(int itemId, int expectedTierMax)
    {
        Assert.True(PetSteppedAttackPowerCategoryTable.TryResolveTierMax(itemId, out var tierMax));
        Assert.Equal(expectedTierMax, tierMax);
    }

    [Theory]
    [InlineData(541)]
    [InlineData(542)]
    [InlineData(547)]
    [InlineData(560)]
    [InlineData(543)]
    [InlineData(544)]
    [InlineData(548)]
    [InlineData(561)]
    [InlineData(1452)]
    [InlineData(86819)]
    [InlineData(545)]
    [InlineData(549)]
    [InlineData(562)]
    [InlineData(86820)]
    [InlineData(546)]
    [InlineData(550)]
    public void TryResolveTierMax_IdPresentOnlyInTimerGateTable_IsAbsentHere(int itemId)
    {
        Assert.False(PetSteppedAttackPowerCategoryTable.TryResolveTierMax(itemId, out var tierMax));
        Assert.Equal(0, tierMax);
    }

    [Fact]
    public void TryResolveTierMax_UnrecognizedId_ReturnsFalse()
    {
        Assert.False(PetSteppedAttackPowerCategoryTable.TryResolveTierMax(999_999, out var tierMax));
        Assert.Equal(0, tierMax);
    }
}
