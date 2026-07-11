using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Regression coverage for the G12 "LifeUp" set-bonus term (<c>ReturnSetItemIUValue_LifeUp</c>,
///     <c>StatCalculator.Life.cs</c>'s <c>ComputeG12LifeUpBonus</c>, exercised here only through the public
///     <see cref="StatCalculator.ComputeBaseStats" /> entry point since the helper itself is private).
/// </summary>
/// <remarks>
///     Contract: wave13/B4-set-lifeup-threshold. The contract's central finding is that the legacy source
///     contains two mutually exclusive qualifying-slot-count branches (a four-of-six branch and a six-of-six
///     branch), gated by a macro that is textually inert in this translation unit -- so the four-of-six branch
///     is dead code in every real build, and the six-of-six branch (all 6 examined slots must individually
///     qualify) is the only one that ever runs. <see cref="StatCalculator.Life.cs" />'s existing
///     <c>ComputeG12LifeUpBonus</c> already implements the six-of-six threshold (<c>v5 &gt;= 6</c> /
///     <c>v7 &gt;= 6</c>, where the max achievable count is 6) -- this file locks that behavior in with a
///     dedicated regression suite, since no test previously existed for this specific getter. No production
///     code change was needed: the contract confirmed the existing &gt;=6 threshold is correct, not a bug.
///     Six examined slots (Server/Header/Protocol/STRUCT.h:1662-1676, literal-index convention): amulet(0),
///     armor(2), gloves(3), ring(4), boots(5), weapon(7).
/// </remarks>
public class SetLifeUpThresholdTests
{
    // The six examined slot indices, literal legacy equip-array convention (amulet/armor/gloves/ring/boots/weapon).
    private static readonly int[] SixSlots = [0, 2, 3, 4, 5, 7];

    private static CharacterBaseAttributes Attributes()
    {
        // Vitality 0 so the vitality*20 term contributes nothing; previousTribe 0 but every test item id used
        // below sits well outside the G12-custom-set id range (84500-84699 for previousTribe 0) and outside
        // every NXT tribe catalog (77000-77023), so neither of those unrelated bonuses can leak into MaxLife.
        return new CharacterBaseAttributes(0, 0, 0, 0, 1, 0, 0, 0, 0, 0);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels()
    {
        // Level factor 0 so MaxLife's only non-zero contribution in these tests is the LifeUp term under test.
        var dict = new Dictionary<short, LevelRowDto> { [1] = new(1, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0) };
        return dict.ToFrozenDictionary();
    }

    /// <summary>
    ///     A qualifying G12 LifeUp candidate: MartialLevelLimit==12, Sort deliberately NOT 1 or 4 (so it never
    ///     counts as the separate "Legendary set" classification, which would add its own unrelated +30000 flat
    ///     term and confound the assertion), item id far outside every unrelated set/NXT id range used above.
    /// </summary>
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

    /// <summary>Same shape as <see cref="QualifyingItem" /> but MartialLevelLimit deliberately != 12 -- disqualified.</summary>
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

    /// <summary>Builds all 6 examined slots as qualifying candidates at the given per-slot Combine ("IU") value.</summary>
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

        // 5000 (lower per-slot-value threshold) + 15000 (stricter threshold) = 20000, per contract Outputs.
        Assert.Equal(20000, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_NoSlotsQualify_GrantsNothing()
    {
        var equipment = SixSlots.Select(slot => Equip(slot, NonQualifyingItem(90000 + slot), 12)).ToArray();

        var stats = StatCalculator.ComputeBaseStats(Attributes(), equipment, Levels());

        Assert.Equal(0, stats.MaxLife);
    }

    /// <summary>
    ///     Only 5 of the 6 examined slots qualify (the 6th fails the MartialLevelLimit==12 filter) even though
    ///     every qualifying slot's Combine reaches the stricter (12) per-slot value test. The contract's central
    ///     finding is that the dead branch (four-of-six) never compiles in any real build and the live branch
    ///     requires literally all six -- this pins that five-of-six (a fortiori four-of-six) grants nothing,
    ///     not a partial/graded bonus.
    /// </summary>
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

    /// <summary>
    ///     Only 4 of the 6 examined slots qualify -- the exact count the (dead, never-compiled) alternate
    ///     legacy branch would have accepted. Must still grant nothing under the live six-of-six-only branch.
    /// </summary>
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

    /// <summary>
    ///     All six slots qualify and meet the lower per-slot value test, but only 5 of the 6 reach the
    ///     stricter (12) test -- the lower term still fires (all 6 met that test), the stricter term does not
    ///     (not all 6 met it), yielding exactly 5000, never a partial amount between 5000 and 20000.
    /// </summary>
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
