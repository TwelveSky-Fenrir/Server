using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B7 -- ornament (gold/silver) fixed stat bonuses (MyFactor::IsUsedOrnament +
///     MyFactor::GetUsedOrnament, Server/Header/Protocol/MyFactor.cpp:513-547). The bonus is a flat, table-selected
///     whole number folded into each of the four BASE stats (Max Life / Max Mana / Attack Power / Defense Power);
///     there is no scaling or interpolation, so these are exact reference vectors.
///     <para>
///         Reachability: the Wave-6 getter-wiring pass folded these contributions into the four base-stat getters,
///         so the public <see cref="StatCalculator.ComputeBaseStats" /> pipeline now grants the full gold/silver
///         delta once <see cref="ZoneContext.OrnamentInUse" /> is armed and all four decoration slots (9-12) hold
///         an item -- see <see cref="Ornament_ThroughComputeBaseStats_YieldsCitedGoldDeltas" />. Note: in
///         production this still stays 0 today regardless, because <see cref="ZoneContext.OrnamentGoldTimeRemaining" />/
///         <see cref="ZoneContext.OrnamentSilverTimeRemaining" /> have no <c>PlayerRuntimeState</c> source field
///         yet (<c>EquipmentService.AssembleStatContexts</c> never populates them) -- see
///         <c>StatCalculator.OrnamentContribution.cs</c>'s own openQuestions.
///     </para>
/// </summary>
public class OrnamentContributionTests
{
    // ---- helpers ----

    // Minimal ItemRowDto: only ItemId is meaningful for the ornament gate (each decoration slot must hold id > 0).
    private static ItemRowDto Item(int itemId)
    {
        return new ItemRowDto(
            itemId, $"Deco{itemId}", null, null, null,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    private static EquippedItemSlot Slot(int index, int itemId)
    {
        return new EquippedItemSlot(index, Item(itemId), 0, 0, 0, 0);
    }

    // A length-13 slot lookup with the four decoration slots (9-12) occupied by valid items -- the gate's
    // occupied-slot requirement satisfied.
    private static EquippedItemSlot?[] FullDecoSlots()
    {
        var slots = new EquippedItemSlot?[13];
        for (var i = 9; i <= 12; i++)
            slots[i] = Slot(i, 1000 + i);
        return slots;
    }

    private static EquippedItemSlot?[] EmptySlots()
    {
        return new EquippedItemSlot?[13];
    }

    // ---- value table (MyFactor.cpp:530-533 gold / :540-543 silver) ----

    [Fact]
    public void OrnamentBonusTable_HasExactlyTheFourSelectorsWithCitedValues()
    {
        Assert.Equal(4, StatCalculator.OrnamentBonusTable.Count);

        Assert.Equal(new StatCalculator.OrnamentBonusRow(825, 550),
            StatCalculator.OrnamentBonusTable[StatCalculator.OrnamentSort.Hp]);
        Assert.Equal(new StatCalculator.OrnamentBonusRow(750, 500),
            StatCalculator.OrnamentBonusTable[StatCalculator.OrnamentSort.Mp]);
        Assert.Equal(new StatCalculator.OrnamentBonusRow(413, 215),
            StatCalculator.OrnamentBonusTable[StatCalculator.OrnamentSort.Dmg]);
        Assert.Equal(new StatCalculator.OrnamentBonusRow(825, 550),
            StatCalculator.OrnamentBonusTable[StatCalculator.OrnamentSort.Def]);
    }

    [Fact]
    public void OrnamentDecorationSlots_AreExactlyEntriesNineThroughTwelve()
    {
        Assert.Equal(4, StatCalculator.OrnamentDecorationSlots.Count);
        foreach (var expected in (int[])[9, 10, 11, 12])
            Assert.Contains(expected, StatCalculator.OrnamentDecorationSlots);

        // The amulet slot (0) and pet slot (8) are deliberately NOT part of the gate.
        Assert.DoesNotContain(0, StatCalculator.OrnamentDecorationSlots);
        Assert.DoesNotContain(8, StatCalculator.OrnamentDecorationSlots);
    }

    [Theory]
    [InlineData(StatCalculator.OrnamentSort.Hp, 825)]
    [InlineData(StatCalculator.OrnamentSort.Mp, 750)]
    [InlineData(StatCalculator.OrnamentSort.Dmg, 413)]
    [InlineData(StatCalculator.OrnamentSort.Def, 825)]
    public void GetOrnamentBonus_GoldTier_ReturnsCitedGoldValue(StatCalculator.OrnamentSort sort, int expected)
    {
        Assert.Equal(expected, StatCalculator.GetOrnamentBonus(sort, StatCalculator.OrnamentTier.Gold));
    }

    [Theory]
    [InlineData(StatCalculator.OrnamentSort.Hp, 550)]
    [InlineData(StatCalculator.OrnamentSort.Mp, 500)]
    [InlineData(StatCalculator.OrnamentSort.Dmg, 215)]
    [InlineData(StatCalculator.OrnamentSort.Def, 550)]
    public void GetOrnamentBonus_SilverTier_ReturnsCitedSilverValue(StatCalculator.OrnamentSort sort, int expected)
    {
        Assert.Equal(expected, StatCalculator.GetOrnamentBonus(sort, StatCalculator.OrnamentTier.Silver));
    }

    [Theory]
    [InlineData(StatCalculator.OrnamentSort.Hp)]
    [InlineData(StatCalculator.OrnamentSort.Mp)]
    [InlineData(StatCalculator.OrnamentSort.Dmg)]
    [InlineData(StatCalculator.OrnamentSort.Def)]
    public void GetOrnamentBonus_NotActiveTier_ReturnsZeroForEverySelector(StatCalculator.OrnamentSort sort)
    {
        Assert.Equal(0, StatCalculator.GetOrnamentBonus(sort, StatCalculator.OrnamentTier.NotActive));
    }

    [Fact]
    public void GetOrnamentBonus_UnknownSelector_ReturnsZeroEvenWhenTierArmed()
    {
        // A stat selector outside the four recognized codes matches no branch and falls through to 0.
        var unknown = (StatCalculator.OrnamentSort)99;
        Assert.Equal(0, StatCalculator.GetOrnamentBonus(unknown, StatCalculator.OrnamentTier.Gold));
        Assert.Equal(0, StatCalculator.GetOrnamentBonus(unknown, StatCalculator.OrnamentTier.Silver));
    }

    // ---- tier resolution (IsUsedOrnament, MyFactor.cpp:513-521) ----

    [Fact]
    public void ResolveOrnamentTier_ArmedGoldPositive_ReturnsGold()
    {
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);
        Assert.Equal(StatCalculator.OrnamentTier.Gold, StatCalculator.ResolveOrnamentTier(zone, FullDecoSlots()));
    }

    [Fact]
    public void ResolveOrnamentTier_GoldPositive_BeatsSilverStrictly()
    {
        // Both counters positive -> gold wins regardless of silver (the dangling-else binds to the gold test).
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 1, OrnamentSilverTimeRemaining: 999);
        Assert.Equal(StatCalculator.OrnamentTier.Gold, StatCalculator.ResolveOrnamentTier(zone, FullDecoSlots()));
    }

    [Fact]
    public void ResolveOrnamentTier_GoldNotPositiveButSilverPositive_ReturnsSilver()
    {
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 0, OrnamentSilverTimeRemaining: 50);
        Assert.Equal(StatCalculator.OrnamentTier.Silver, StatCalculator.ResolveOrnamentTier(zone, FullDecoSlots()));
    }

    [Fact]
    public void ResolveOrnamentTier_ArmedButBothCountersNonPositive_ReturnsNotActive()
    {
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 0, OrnamentSilverTimeRemaining: 0);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive,
            StatCalculator.ResolveOrnamentTier(zone, FullDecoSlots()));
    }

    [Fact]
    public void ResolveOrnamentTier_NegativeCounters_ReturnNotActive()
    {
        // "greater than zero" -- a non-positive (including negative) counter selects nothing.
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: -5, OrnamentSilverTimeRemaining: -1);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive,
            StatCalculator.ResolveOrnamentTier(zone, FullDecoSlots()));
    }

    [Fact]
    public void ResolveOrnamentTier_ActiveFlagOff_ReturnsNotActiveEvenWithGold()
    {
        var zone = new ZoneContext(OrnamentInUse: false, OrnamentGoldTimeRemaining: 100);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive,
            StatCalculator.ResolveOrnamentTier(zone, FullDecoSlots()));
    }

    [Fact]
    public void ResolveOrnamentTier_AllDecorationSlotsEmpty_ReturnsNotActive()
    {
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive, StatCalculator.ResolveOrnamentTier(zone, EmptySlots()));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void ResolveOrnamentTier_AnySingleDecorationSlotEmpty_ReturnsNotActive(int missingSlot)
    {
        var slots = FullDecoSlots();
        slots[missingSlot] = null;
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive, StatCalculator.ResolveOrnamentTier(zone, slots));
    }

    [Fact]
    public void ResolveOrnamentTier_DecorationSlotItemIdZero_ReturnsNotActive()
    {
        // An occupied slot whose item id is not strictly greater than zero fails the gate.
        var slots = FullDecoSlots();
        slots[11] = Slot(11, 0);
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive, StatCalculator.ResolveOrnamentTier(zone, slots));
    }

    [Fact]
    public void ResolveOrnamentTier_AmuletAndPetSlotsOccupiedButDecorationsEmpty_ReturnsNotActive()
    {
        // The gate requires the four decoration slots specifically -- amulet (0) and pet (8) do not count.
        var slots = EmptySlots();
        slots[0] = Slot(0, 5000);
        slots[8] = Slot(8, 6000);
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);
        Assert.Equal(StatCalculator.OrnamentTier.NotActive, StatCalculator.ResolveOrnamentTier(zone, slots));
    }

    // ---- per-stat contribution wrappers ----

    [Fact]
    public void Contributions_GoldTier_YieldCitedGoldValues()
    {
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);
        var slots = FullDecoSlots();

        Assert.Equal(825, StatCalculator.OrnamentLifeContribution(zone, slots));
        Assert.Equal(750, StatCalculator.OrnamentManaContribution(zone, slots));
        Assert.Equal(413, StatCalculator.OrnamentAttackContribution(zone, slots));
        Assert.Equal(825, StatCalculator.OrnamentDefenseContribution(zone, slots));
    }

    [Fact]
    public void Contributions_SilverTier_YieldCitedSilverValues()
    {
        var zone = new ZoneContext(OrnamentInUse: true, OrnamentSilverTimeRemaining: 100);
        var slots = FullDecoSlots();

        Assert.Equal(550, StatCalculator.OrnamentLifeContribution(zone, slots));
        Assert.Equal(500, StatCalculator.OrnamentManaContribution(zone, slots));
        Assert.Equal(215, StatCalculator.OrnamentAttackContribution(zone, slots));
        Assert.Equal(550, StatCalculator.OrnamentDefenseContribution(zone, slots));
    }

    [Fact]
    public void Contributions_NotActive_YieldZero()
    {
        var zone = new ZoneContext(OrnamentInUse: false, OrnamentGoldTimeRemaining: 100);
        var slots = FullDecoSlots();

        Assert.Equal(0, StatCalculator.OrnamentLifeContribution(zone, slots));
        Assert.Equal(0, StatCalculator.OrnamentManaContribution(zone, slots));
        Assert.Equal(0, StatCalculator.OrnamentAttackContribution(zone, slots));
        Assert.Equal(0, StatCalculator.OrnamentDefenseContribution(zone, slots));
    }

    [Fact]
    public void Contributions_DefaultZoneContext_YieldZero()
    {
        // A default ZoneContext (no ornament) never contributes -- the neutral seam case.
        var slots = FullDecoSlots();
        Assert.Equal(0, StatCalculator.OrnamentLifeContribution(default, slots));
        Assert.Equal(0, StatCalculator.OrnamentManaContribution(default, slots));
        Assert.Equal(0, StatCalculator.OrnamentAttackContribution(default, slots));
        Assert.Equal(0, StatCalculator.OrnamentDefenseContribution(default, slots));
    }

    // ---- end-to-end wiring guard (Wave-6 getter wiring landed -- see class remarks) ----

    [Fact]
    public void Ornament_ThroughComputeBaseStats_YieldsCitedGoldDeltas()
    {
        // The four base-stat getters now call the ornament contribution methods (Wave-6 getter wiring). A
        // fully-armed gold ornament (all four decoration slots occupied) must add exactly the cited gold deltas
        // on top of the equipment-only baseline, and nothing else -- every other zone-gated term (elixir,
        // stellar core, rank-buff, drunk) stays at its own neutral default for both calls, so the ONLY
        // difference between baseline and withArmedOrnament is the ornament bonus itself.
        var attributes = new CharacterBaseAttributes(
            Vitality: 100, Strength: 100, Intelligence: 50, Dexterity: 50,
            Level: 1, Tribe: 0, PreviousTribe: 0, Title: 0, Halo: 0, RebirthCount: 0);
        var levels = new Dictionary<short, LevelRowDto>
        {
            [1] = new(1, 0, 100, 0, 0, 0, 0, 0, 0, 50, 100)
        }.ToFrozenDictionary();

        EquippedItemSlot[] decoEquipment =
        [
            Slot(9, 1009), Slot(10, 1010), Slot(11, 1011), Slot(12, 1012)
        ];

        var armedGold = new ZoneContext(ZoneNumber: 1, OrnamentInUse: true, OrnamentGoldTimeRemaining: 100);

        var baseline = StatCalculator.ComputeBaseStats(attributes, decoEquipment, levels);
        var withArmedOrnament = StatCalculator.ComputeBaseStats(attributes, decoEquipment, levels, zone: armedGold);

        Assert.Equal(baseline.MaxLife + 825, withArmedOrnament.MaxLife);
        Assert.Equal(baseline.MaxMana + 750, withArmedOrnament.MaxMana);
        Assert.Equal(baseline.AttackPower + 413, withArmedOrnament.AttackPower);
        Assert.Equal(baseline.DefensePower + 825, withArmedOrnament.DefensePower);
    }
}
