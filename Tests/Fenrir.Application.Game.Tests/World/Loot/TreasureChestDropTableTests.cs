using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Coverage for <see cref="TreasureChestDropTable" />, the C14 port of <c>MyUtil::ProcessForDropTresureChest</c>'s
///     single-0-99-roll five-outcome weighted table (78% 1145 / 10% 8109 / 8% pet / 3% 8110 / 1% 695).
/// </summary>
public class TreasureChestDropTableTests
{
    [Theory]
    [InlineData(0, TreasureChestDropTable.JackpotItemId)]
    [InlineData(77, TreasureChestDropTable.JackpotItemId)]
    [InlineData(78, TreasureChestDropTable.SecondItemId)]
    [InlineData(87, TreasureChestDropTable.SecondItemId)]
    [InlineData(96, TreasureChestDropTable.FourthItemId)]
    [InlineData(98, TreasureChestDropTable.FourthItemId)]
    [InlineData(99, TreasureChestDropTable.RareItemId)]
    public void Resolve_FixedItemThresholds_MapToExpectedItemId(int roll, int expectedItemId)
    {
        var outcome = TreasureChestDropTable.Resolve(roll);

        Assert.Equal(TreasureChestOutcomeKind.FixedItem, outcome.Kind);
        Assert.Equal(expectedItemId, outcome.ItemId);
    }

    [Theory]
    [InlineData(88)]
    [InlineData(91)]
    [InlineData(95)]
    public void Resolve_PetBand_YieldsRandomLevel1PetMarker(int roll)
    {
        var outcome = TreasureChestDropTable.Resolve(roll);

        Assert.Equal(TreasureChestOutcomeKind.RandomLevel1Pet, outcome.Kind);
        Assert.Equal(0, outcome.ItemId); // marker -- caller draws from Level1PetPool
    }

    [Fact]
    public void Weighting_AcrossEveryRoll_MatchesTheFivePercentages()
    {
        var jackpot = 0;
        var second = 0;
        var pet = 0;
        var fourth = 0;
        var rare = 0;

        for (var roll = 0; roll < TreasureChestDropTable.RollExclusiveUpperBound; roll++)
        {
            var outcome = TreasureChestDropTable.Resolve(roll);
            if (outcome.Kind == TreasureChestOutcomeKind.RandomLevel1Pet)
                pet++;
            else if (outcome.ItemId == TreasureChestDropTable.JackpotItemId)
                jackpot++;
            else if (outcome.ItemId == TreasureChestDropTable.SecondItemId)
                second++;
            else if (outcome.ItemId == TreasureChestDropTable.FourthItemId)
                fourth++;
            else if (outcome.ItemId == TreasureChestDropTable.RareItemId)
                rare++;
        }

        Assert.Equal(78, jackpot);
        Assert.Equal(10, second);
        Assert.Equal(8, pet);
        Assert.Equal(3, fourth);
        Assert.Equal(1, rare);
        Assert.Equal(100, jackpot + second + pet + fourth + rare); // no remainder bucket
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Resolve_RollOutsideZeroToNinetyNine_Throws(int roll)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TreasureChestDropTable.Resolve(roll));
    }

    [Fact]
    public void Roll_UsesPlainUniformDraw_DeterministicForAFixedSeed()
    {
        // Two Random(seed) instances yield the same first draw, so Roll must agree with a direct Resolve of it.
        var expected = TreasureChestDropTable.Resolve(new Random(12345).Next(TreasureChestDropTable.RollExclusiveUpperBound));
        var actual = TreasureChestDropTable.Roll(new Random(12345));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Level1PetPool_IsEmpty_UntilTheUnrecoveredListIsSupplied()
    {
        Assert.Empty(TreasureChestDropTable.Level1PetPool);
    }
}
