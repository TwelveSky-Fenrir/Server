using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Tests.AntiCheat;

public class TribeConsistencyValidatorTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public void LegalPairs_AreConsistent(int tribe, int previousTribe)
    {
        Assert.True(TribeConsistencyValidator.IsConsistent(tribe, previousTribe));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(3, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 0)]
    [InlineData(-1, 0)]
    public void IllegalPairs_AreRejected(int tribe, int previousTribe)
    {
        Assert.False(TribeConsistencyValidator.IsConsistent(tribe, previousTribe));
    }
}
