using Fenrir.Application.Game.Domain.Hotkeys;

namespace Fenrir.Application.Game.Tests.Hotkeys;

public class BotHotKeyResupplyPolicyTests
{
    private static readonly BotHotKeyResupplyPolicy.BoundCategories NothingBound = new(false, false, false, false,
        false);

    private static readonly BotHotKeyResupplyPolicy.HotkeyAddress[] TwoEmptySlots =
    [
        new(0, 0), new(0, 1)
    ];

    private static BotHotKeyResupplyPolicy.InventoryCandidate Candidate(byte slot,
        BotHotKeyResupplyPolicy.ResupplyCategory category, int itemId = 100, int qty = 7)
    {
        return new BotHotKeyResupplyPolicy.InventoryCandidate(0, slot, itemId, qty, category);
    }

    [Theory]
    [InlineData(1, BotHotKeyResupplyPolicy.ResupplyCategory.Hp)]
    [InlineData(2, BotHotKeyResupplyPolicy.ResupplyCategory.Hp)]
    [InlineData(3, BotHotKeyResupplyPolicy.ResupplyCategory.Mp)]
    [InlineData(4, BotHotKeyResupplyPolicy.ResupplyCategory.Mp)]
    [InlineData(5, BotHotKeyResupplyPolicy.ResupplyCategory.HpMp)]
    [InlineData(0, BotHotKeyResupplyPolicy.ResupplyCategory.None)]
    [InlineData(9, BotHotKeyResupplyPolicy.ResupplyCategory.None)]
    [InlineData(16, BotHotKeyResupplyPolicy.ResupplyCategory.None)]
    public void ClassifyHpMpByPotionType_MapsOnlyTheCitedTypes(int potionType1,
        BotHotKeyResupplyPolicy.ResupplyCategory expected)
    {
        Assert.Equal(expected, BotHotKeyResupplyPolicy.ClassifyHpMpByPotionType(potionType1));
    }

    [Fact]
    public void NoHpBound_HpSourceAndEmptySlot_MovesTheWholeStack()
    {
        var moves = BotHotKeyResupplyPolicy.Resolve(
            NothingBound,
            [Candidate(5, BotHotKeyResupplyPolicy.ResupplyCategory.Hp, 1364, 9)],
            TwoEmptySlots,
            false, false, false, false);

        var move = Assert.Single(moves);
        Assert.Equal(0, move.SourcePage);
        Assert.Equal(5, move.SourceSlot);
        Assert.Equal(0, move.DestinationPage);
        Assert.Equal(0, move.DestinationIndex);
        Assert.Equal(1364, move.ItemId);
        Assert.Equal(9, move.Quantity);
    }

    [Fact]
    public void HpAlreadyBound_NoHpRefill()
    {
        var bound = NothingBound with { HasHp = true };

        var moves = BotHotKeyResupplyPolicy.Resolve(bound,
            [Candidate(5, BotHotKeyResupplyPolicy.ResupplyCategory.Hp)], TwoEmptySlots,
            false, false, false, false);

        Assert.Empty(moves);
    }

    [Fact]
    public void SharedHpMpBound_SatisfiesBothHpAndMpChecks_NoRefill()
    {
        var bound = NothingBound with { HasHpMp = true };

        var moves = BotHotKeyResupplyPolicy.Resolve(bound,
            [
                Candidate(1, BotHotKeyResupplyPolicy.ResupplyCategory.Hp),
                Candidate(2, BotHotKeyResupplyPolicy.ResupplyCategory.Mp)
            ],
            TwoEmptySlots, false, false, false, false);

        Assert.Empty(moves);
    }

    [Fact]
    public void SharedHpMpSource_SatisfiesTheHpRefill()
    {
        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound,
            [Candidate(3, BotHotKeyResupplyPolicy.ResupplyCategory.HpMp)], TwoEmptySlots,
            false, false, false, false);

        var move = Assert.Single(moves);
        Assert.Equal(3, move.SourceSlot);
    }

    [Fact]
    public void HpAndMpBothNeeded_TwoSources_ConsumeDistinctEmptySlots()
    {
        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound,
            [
                Candidate(1, BotHotKeyResupplyPolicy.ResupplyCategory.Hp, 10),
                Candidate(2, BotHotKeyResupplyPolicy.ResupplyCategory.Mp, 20)
            ],
            TwoEmptySlots, false, false, false, false);

        Assert.Equal(2, moves.Length);
        Assert.Equal((byte)0, moves[0].DestinationIndex);
        Assert.Equal((byte)1, moves[1].DestinationIndex);
        Assert.Equal(10, moves[0].ItemId);
        Assert.Equal(20, moves[1].ItemId);
    }

    [Fact]
    public void NoEmptyHotkeySlot_NothingMoves()
    {
        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound,
            [Candidate(1, BotHotKeyResupplyPolicy.ResupplyCategory.Hp)],
            [], false, false, false, false);

        Assert.Empty(moves);
    }

    [Fact]
    public void NoMatchingSourceForOneCategory_OtherCategoriesStillRefill()
    {
        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound,
            [Candidate(2, BotHotKeyResupplyPolicy.ResupplyCategory.Mp, 20)],
            TwoEmptySlots, false, false, false, false);

        var move = Assert.Single(moves);
        Assert.Equal(20, move.ItemId);
        Assert.Equal((byte)0, move.DestinationIndex);
    }

    [Fact]
    public void PetPrey_RequiresFlagAndEquippedPet()
    {
        var candidates = new[] { Candidate(4, BotHotKeyResupplyPolicy.ResupplyCategory.PetPrey, 40) };

        Assert.Empty(BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, TwoEmptySlots,
            false, true, false, false));
        Assert.Empty(BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, TwoEmptySlots,
            true, false, false, false));

        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, TwoEmptySlots,
            true, true, false, false);
        Assert.Equal(40, Assert.Single(moves).ItemId);
    }

    [Fact]
    public void PetPreyAlreadyBound_NoRefill_EvenWithFlagAndPet()
    {
        var bound = NothingBound with { HasPetPrey = true };

        var moves = BotHotKeyResupplyPolicy.Resolve(bound,
            [Candidate(4, BotHotKeyResupplyPolicy.ResupplyCategory.PetPrey)], TwoEmptySlots,
            true, true, false, false);

        Assert.Empty(moves);
    }

    [Fact]
    public void PetFood_RequiresFlagAndPresentAnimal()
    {
        var candidates = new[] { Candidate(6, BotHotKeyResupplyPolicy.ResupplyCategory.PetFood, 60) };

        Assert.Empty(BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, TwoEmptySlots,
            false, false, false, true));
        Assert.Empty(BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, TwoEmptySlots,
            false, false, true, false));

        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, TwoEmptySlots,
            false, false, true, true);
        Assert.Equal(60, Assert.Single(moves).ItemId);
    }

    [Fact]
    public void AllFourCategories_FireInOrder_WhenEverythingIsAvailable()
    {
        BotHotKeyResupplyPolicy.HotkeyAddress[] fourEmpty = [new(0, 0), new(0, 1), new(0, 2), new(0, 3)];
        var candidates = new[]
        {
            Candidate(0, BotHotKeyResupplyPolicy.ResupplyCategory.Hp, 10),
            Candidate(1, BotHotKeyResupplyPolicy.ResupplyCategory.Mp, 20),
            Candidate(2, BotHotKeyResupplyPolicy.ResupplyCategory.PetPrey, 30),
            Candidate(3, BotHotKeyResupplyPolicy.ResupplyCategory.PetFood, 40)
        };

        var moves = BotHotKeyResupplyPolicy.Resolve(NothingBound, candidates, fourEmpty,
            true, true, true, true);

        Assert.Equal(4, moves.Length);
        Assert.Equal(10, moves[0].ItemId);
        Assert.Equal(20, moves[1].ItemId);
        Assert.Equal(30, moves[2].ItemId);
        Assert.Equal(40, moves[3].ItemId);
        Assert.Equal((byte)3, moves[3].DestinationIndex);
    }
}
