using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Tests.AntiCheat;

/// <summary>
///     Covers <see cref="TribeConsistencyValidator.IsConsistent" /> — the avatar-registration tribe/previous-tribe
///     self-consistency guard (Server/ts25zone/S04_MyWork02.cpp:880-901).
/// </summary>
public class TribeConsistencyValidatorTests
{
    [Theory]
    // Main-faction tribes (0,1,2): PreviousTribe must equal Tribe.
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    // Fujin (tribe 3): PreviousTribe must be one of the three main factions it converted from.
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public void LegalPairs_AreConsistent(int tribe, int previousTribe)
    {
        Assert.True(TribeConsistencyValidator.IsConsistent(tribe, previousTribe));
    }

    [Theory]
    // Main-faction mismatch.
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    // A main-faction tribe may never have PreviousTribe == 3.
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    // Fujin (3) may never claim PreviousTribe 3 (or anything outside 0..2).
    [InlineData(3, 3)]
    [InlineData(3, 4)]
    // Any tribe value outside {0,1,2,3} is illegal regardless of PreviousTribe.
    [InlineData(4, 0)]
    [InlineData(-1, 0)]
    public void IllegalPairs_AreRejected(int tribe, int previousTribe)
    {
        Assert.False(TribeConsistencyValidator.IsConsistent(tribe, previousTribe));
    }
}
