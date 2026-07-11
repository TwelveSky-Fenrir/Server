using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class SetLifeUpThresholdTests
{
    private static readonly int[] SixSlots = [0, 2, 3, 4, 5, 7];

    private static CharacterBaseAttributes Attributes()
    {
        return new CharacterBaseAttributes(0, 0, 0, 0, 1, 0, 0, 0, 0, 0);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels()
    {
        var dict = new Dictionary<short, LevelRowDto> { [1] = new(1, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0) };
        return dict.ToFrozenDictionary();
    }

        private static ItemRowDto QualifyingItem(int itemId)
    {
        return new ItemRowDto(itemId, $"Item{itemId}", null, null, null,
            0, 2, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 12,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

        private static ItemRowDto NonQualifyingItem(int itemId)
    {
        return new ItemRowDto(itemId, $"Item{itemId}", null, null, null,
            0, 2, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    private static EquippedItemSlot Equip(int slotIndex, ItemRowDto item, byte combine)
    {
        return new EquippedItemSlot(slotIndex, item, 0, combine, 0, 0);
    }

        private static EquippedItemSlot[] SixQualifyingSlotsAtCombine(byte combine)
    {
        return SixSlots.Select(slot => Equip(slot, QualifyingItem(90000 + slot), combine)).ToArray();
    }

    [Fact]
    public void ComputeBaseStats_AllSixSlotsQualifyAtCombineSix_GrantsFiveThousandOnly()
    {
        var stats = StatCalculator.ComputeBaseStats(Attributes(), SixQualifyingSlotsAtCombine(6), Levels());

        Assert.Equal(5000, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_AllSixSlotsQualifyAtCombineTwelve_GrantsTwentyThousandTotal()
    {
        var stats = StatCalculator.ComputeBaseStats(Attributes(), SixQualifyingSlotsAtCombine(12), Levels());

        Assert.Equal(20000, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_NoSlotsQualify_GrantsNothing()
    {
        var equipment = SixSlots.Select(slot => Equip(slot, NonQualifyingItem(90000 + slot), 12)).ToArray();

        var stats = StatCalculator.ComputeBaseStats(Attributes(), equipment, Levels());

        Assert.Equal(0, stats.MaxLife);
    }

        [Fact]
    public void ComputeBaseStats_FiveOfSixSlotsQualify_GrantsNothingNotAPartialBonus()
    {
        var equipment = SixSlots
            .Select((slot, i) => i == 0
                ? Equip(slot, NonQualifyingItem(90000 + slot), 12)
                : Equip(slot, QualifyingItem(90000 + slot), 12))
            .ToArray();

        var stats = StatCalculator.ComputeBaseStats(Attributes(), equipment, Levels());

        Assert.Equal(0, stats.MaxLife);
    }

        [Fact]
    public void ComputeBaseStats_FourOfSixSlotsQualify_GrantsNothingDespiteMatchingTheDeadBranchsThreshold()
    {
        var equipment = SixSlots
            .Select((slot, i) => i < 2
                ? Equip(slot, NonQualifyingItem(90000 + slot), 12)
                : Equip(slot, QualifyingItem(90000 + slot), 12))
            .ToArray();

        var stats = StatCalculator.ComputeBaseStats(Attributes(), equipment, Levels());

        Assert.Equal(0, stats.MaxLife);
    }

        [Fact]
    public void ComputeBaseStats_AllSixQualifyButOnlyFiveReachStricterValue_GrantsOnlyTheLowerTerm()
    {
        var equipment = SixSlots
            .Select((slot, i) => Equip(slot, QualifyingItem(90000 + slot), (byte)(i == 0 ? 6 : 12)))
            .ToArray();

        var stats = StatCalculator.ComputeBaseStats(Attributes(), equipment, Levels());

        Assert.Equal(5000, stats.MaxLife);
    }
}
