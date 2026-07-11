using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B6 follow-up (contract wave14/B6-costume-enchant-date-source) -- closes the "aCostumeDate has
///     no PlayerRuntimeState source field yet" gap: <see cref="EquipmentService.RecomputeStats" /> (via its
///     private <c>AssembleStatContexts</c> assembly point) previously hardcoded
///     <c>CosmeticContext.CostumeEnchantCs</c> to 0 regardless of <see cref="PlayerRuntimeState.CostumeIndex" />.
///     <see cref="PlayerRuntimeState.CostumeDate" /> (the ten packed enchant-slot words, index-aligned with
///     <see cref="PlayerRuntimeState.CostumeWardrobe" />) is the new source field this workstream adds.
///     <para>
///         This exercises the wiring end-to-end through <see cref="EquipmentService.RecomputeStats" /> rather
///         than re-testing <c>StatCalculator.DecodeCostumeEnchantCs</c>'s own decode math (already covered
///         exhaustively by <c>CostumeContributionTests</c>). <see cref="Fenrir.Application.Game.Stats.EffectiveStats.Critical" />/
///         <see cref="Fenrir.Application.Game.Stats.EffectiveStats.Luck" /> are used as the observable deltas: both are
///         direct, unwrapped additions of the decoded <c>cs</c> magnitude
///         (<c>StatCalculator.CostumeCriticalContribution</c>/<c>CostumeLuckContribution</c>), unlike
///         Vitality/Strength/Ki/Wisdom which only ever surface indirectly through further level-table-scaled
///         derived stats.
///     </para>
/// </summary>
public class CostumeEnchantWiringTests
{
    private static CharacterBaseAttributes NoBonusAttributes()
    {
        return new CharacterBaseAttributes(0, 0, 0, 0, 1, 0, 0, 0, 0, 0);
    }

    private static PlayerRuntimeState State(int costumeIndex, ImmutableArray<int> costumeDate)
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1,
            CostumeIndex = costumeIndex,
            CostumeDate = costumeDate
        };
    }

    [Fact]
    public void CostumeDate_DefaultsToTenZeroSlots()
    {
        var state = new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1
        };

        // Same "no acquisition path yet" posture as CostumeWardrobe: a fresh character has ten zero slots.
        Assert.Equal(10, state.CostumeDate.Length);
        Assert.All(state.CostumeDate, v => Assert.Equal(0, v));

        // CostumeIndex's own pre-existing "nothing worn" default -- load-bearing together with CostumeDate for
        // the "untouched character is unaffected" guarantee the next test asserts.
        Assert.Equal(-1, state.CostumeIndex);
    }

    [Fact]
    public void RecomputeStats_DefaultCostumeIndex_IgnoresPopulatedCostumeDate()
    {
        // CostumeIndex stays at its -1 "nothing worn" default even though CostumeDate carries real, non-zero
        // packed words -- DecodeCostumeEnchantCs's own index-range gate (10-19) must zero the contribution
        // regardless, so a character nobody has equipped/enchanted a costume for sees no behavior change from
        // this workstream landing.
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> populatedDate = [11, 22, 33, 44, 55, 66, 77, 88, 99, 111];

        var withoutState = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withUnwornCostume =
            EquipmentService.RecomputeStats(attributes, equipment, worldData, runtimeState: State(-1, populatedDate));

        Assert.Equal(withoutState, withUnwornCostume);
    }

    [Theory]
    [InlineData(0)] // "selected" range (0-9) -- not worn; the gate requires 10-19
    [InlineData(9)] // top of the selected range
    [InlineData(20)] // out of range entirely
    public void RecomputeStats_IndexOutsideWornRange_ContributesNothing(int costumeIndex)
    {
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> populatedDate = [50, 50, 50, 50, 50, 50, 50, 50, 50, 50];

        var withoutState = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withState = EquipmentService.RecomputeStats(attributes, equipment, worldData,
            runtimeState: State(costumeIndex, populatedDate));

        Assert.Equal(withoutState, withState);
    }

    [Fact]
    public void RecomputeStats_WornCostumeIndex_DecodesSlotAndAddsCriticalAndLuck()
    {
        // Index 10 -> slot 0 (10 % 10). Slot 0 carries low byte 50: Critical += 50/10 = 5
        // (StatCalculator.CostumeCriticalContribution's ordinary positive-magnitude rule, not the 96 sentinel);
        // Luck += 50*2 = 100, with no extra +100 valid-costume bonus since CostumeNumber stays 0 (not a
        // cataloged/valid costume id) -- StatCalculator.CostumeLuckContribution.
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> date = [50, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        var baseline = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withWornCostume =
            EquipmentService.RecomputeStats(attributes, equipment, worldData, runtimeState: State(10, date));

        Assert.Equal(baseline.Critical + 5, withWornCostume.Critical);
        Assert.Equal(baseline.Luck + 100, withWornCostume.Luck);

        // Every other top-level stat is untouched by this specific wiring change.
        Assert.Equal(baseline.MaxMana, withWornCostume.MaxMana);
        Assert.Equal(baseline.AttackPower, withWornCostume.AttackPower);
        Assert.Equal(baseline.DefensePower, withWornCostume.DefensePower);
        Assert.Equal(baseline.CriticalDefence, withWornCostume.CriticalDefence);
    }

    [Fact]
    public void RecomputeStats_WornCostumeIndex_ReadsSignedLowByteNegative()
    {
        // Index 19 -> slot 9 (top of the worn range). Low byte 200 reads back as signed -56
        // (StatCalculator.ReadSignedLowByte): Critical's positive-guard yields 0 (no positive magnitude to
        // divide), Luck subtracts twice (cs*2, no positive guard) -- both per StatCalculator.
        // CostumeCriticalContribution/CostumeLuckContribution's own documented asymmetry.
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> date = [0, 0, 0, 0, 0, 0, 0, 0, 0, 200];

        var baseline = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withWornCostume =
            EquipmentService.RecomputeStats(attributes, equipment, worldData, runtimeState: State(19, date));

        Assert.Equal(baseline.Critical, withWornCostume.Critical); // 0 or below contributes nothing
        Assert.Equal(baseline.Luck - 112, withWornCostume.Luck); // -56 * 2
    }
}
