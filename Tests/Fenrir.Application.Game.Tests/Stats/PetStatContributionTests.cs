using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B8 (pet stat contributions) -- the AMULET-pet graded/flat bonuses, the stepped attack
///     bonus, and the grow-percent, ported from PETSYSTEM (Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp).
///     Every method is a pure computation, so these are exact reference vectors. Most of the methods are
///     exercised directly (not through <see cref="StatCalculator.ComputeBaseStats" />) because the
///     getter/skill-pipeline call sites are landed by a separate serial integration pass (see the workstream's
///     wiringManifest), and several remain wiring-blocked (no runtime packed-graded-value field yet). The flat
///     amulet attack/defense tables are the exception: a 2026-07-11 confirmation pass wired the 6
///     non-overlapping ids (8290, 76000-76004) into <see cref="StatCalculator.ComputeBaseStats" />'s
///     AttackPower/DefensePower getters -- see the end-to-end tests below -- while the 3 Phoenix-overlapping
///     ids (76005-76007) stay deliberately excluded at the call site (<c>PetAmuletPhoenixOverlapIds</c>)
///     pending a still-open Attack/Defense-side double-count question (whether <c>ReturnAmuletAttackValue</c>/
///     <c>ReturnAmuletDefenseValue</c>'s own table entry is a third stacking term or a restatement of an
///     existing one). This is a DIFFERENT, narrower question than the Life/Mana base-vs-correction
///     contradiction <c>Domain PetSlotAmuletBonusTable</c> used to flag -- that one is now RESOLVED (workstream
///     pet-amulet-bonus-table-mapping, 2026-07-11: base and correction stack, see <c>StatCalculator.Life.cs</c>
///     and <see cref="B12GetterFixTests" />'s two-pass Life/Mana regression tests).
/// </summary>
public class PetStatContributionTests
{
    private const int AmuletSort = 28;
    private const int GrowPetSort = 22;

    // Tier maxima for the growth-pet stepped-attack / grow-percent tests (Domain PetGrowthCaps indices 0-3).
    private const int Tier0Max = 40_000_000;

    /// <summary>Packs four signed bytes into the sub-field-2 int, low byte = IS.</summary>
    private static int Pack(int isByte, int iuByte = 0, int imByte = 0, int izByte = 0)
    {
        return (isByte & 0xFF) | ((iuByte & 0xFF) << 8) | ((imByte & 0xFF) << 16) | ((izByte & 0xFF) << 24);
    }

    // ---- signed-byte decoders (function.h:317-343) ----

    [Fact]
    public void Decoders_SplitPackedValueIntoFourBytes()
    {
        var packed = Pack(0x11, 0x22, 0x33, 0x44);
        Assert.Equal(0x11, StatCalculator.DecodePetIsByte(packed));
        Assert.Equal(0x22, StatCalculator.DecodePetIuByte(packed));
        Assert.Equal(0x33, StatCalculator.DecodePetImByte(packed));
        Assert.Equal(0x44, StatCalculator.DecodePetIzByte(packed));
    }

    [Fact]
    public void Decoders_AreSigned()
    {
        // A high byte (>= 128) decodes negative, matching the legacy signed widening.
        var packed = Pack(0xFF, 0x80, 0, 0);
        Assert.Equal(-1, StatCalculator.DecodePetIsByte(packed));
        Assert.Equal(-128, StatCalculator.DecodePetIuByte(packed));
    }

    // ---- Graded IS bonus (six per-type ladders, GameSystem_07_Pet.cpp:1064-1325) ----

    // Each row: statType, encoded IS byte (tens=type, units=grade), expected bonus.
    [Theory]
    // type 1 (attack): grade 0 -> 30 ... grade 9 -> 300
    [InlineData(1, 10, 30)]
    [InlineData(1, 15, 180)]
    [InlineData(1, 19, 300)]
    // type 2 (max life): 200 step
    [InlineData(2, 20, 200)]
    [InlineData(2, 29, 2000)]
    // type 3 (hit/block): 20 step
    [InlineData(3, 30, 20)]
    [InlineData(3, 39, 200)]
    // type 4 (max mana): 250 step
    [InlineData(4, 40, 250)]
    [InlineData(4, 49, 2500)]
    // type 5 (element attack): 100 step
    [InlineData(5, 50, 100)]
    [InlineData(5, 59, 1000)]
    // type 6 (element defense): 100 step
    [InlineData(6, 60, 100)]
    [InlineData(6, 69, 1000)]
    public void GradedIsBonus_MatchesLadder(int statType, int isByte, int expected)
    {
        var bonus = StatCalculator.PetGradedIsBonus(petItemId: 76500, petSort: AmuletSort, Pack(isByte), statType);
        Assert.Equal(expected, bonus);
    }

    [Fact]
    public void GradedIsBonus_ZeroWhenTensDigitDoesNotMatchRequestedType()
    {
        // IS byte 25 encodes type 2, so a request for type 1 gets nothing.
        Assert.Equal(0, StatCalculator.PetGradedIsBonus(76500, AmuletSort, Pack(25), statType: 1));
    }

    [Fact]
    public void GradedIsBonus_ZeroForNonAmuletSort()
    {
        Assert.Equal(0, StatCalculator.PetGradedIsBonus(76500, GrowPetSort, Pack(15), statType: 1));
    }

    [Theory]
    [InlineData(2253)]
    [InlineData(2254)]
    [InlineData(2261)]
    [InlineData(2262)]
    [InlineData(2300)]
    [InlineData(2301)]
    public void GradedIsBonus_ZeroForExcludedIndices(int excludedId)
    {
        Assert.Equal(0, StatCalculator.PetGradedIsBonus(excludedId, AmuletSort, Pack(15), statType: 1));
    }

    // ---- Graded IU bonus (raw grade digit, GameSystem_07_Pet.cpp:1327-1351) ----

    [Fact]
    public void GradedIuBonus_ReturnsRawGradeDigitWhenTensMatches()
    {
        // IU byte 27 -> type 2, grade 7.
        Assert.Equal(7, StatCalculator.PetGradedIuBonus(76500, AmuletSort, Pack(0, 27), statType: 2));
    }

    [Fact]
    public void GradedIuBonus_ZeroWhenTensDoesNotMatch()
    {
        Assert.Equal(0, StatCalculator.PetGradedIuBonus(76500, AmuletSort, Pack(0, 27), statType: 3));
    }

    [Fact]
    public void GradedIuBonus_ZeroForExcludedIndex()
    {
        Assert.Equal(0, StatCalculator.PetGradedIuBonus(2300, AmuletSort, Pack(0, 27), statType: 2));
    }

    // ---- Graded IM bonus (five per-type ladders + the type-5 anomaly, GameSystem_07_Pet.cpp:1353-1576) ----

    [Theory]
    [InlineData(1, 10, 30f)]
    [InlineData(1, 19, 300f)]
    [InlineData(2, 20, 200f)]
    [InlineData(2, 29, 2000f)]
    [InlineData(3, 30, 100f)]
    [InlineData(3, 39, 1000f)]
    [InlineData(4, 40, 100f)]
    [InlineData(4, 49, 1000f)]
    public void GradedImBonus_MatchesLadder(int statType, int imByte, float expected)
    {
        var bonus = StatCalculator.PetGradedImBonus(76500, AmuletSort, Pack(0, 0, imByte), statType);
        Assert.Equal(expected, bonus);
    }

    [Theory]
    // type-5 critical ladder: 0.3, 0.3, 0.9, 1.2, ... 3.0 -- grade 0 AND grade 1 both 0.3 (the anomaly).
    [InlineData(50, 0.3f)]
    [InlineData(51, 0.3f)]
    [InlineData(52, 0.9f)]
    [InlineData(53, 1.2f)]
    [InlineData(59, 3.0f)]
    public void GradedImBonus_Type5CriticalReplicatesGrade1Anomaly(int imByte, float expected)
    {
        var bonus = StatCalculator.PetGradedImBonus(76500, AmuletSort, Pack(0, 0, imByte), statType: 5);
        Assert.Equal(expected, bonus);
    }

    [Fact]
    public void GradedImBonus_HasNoType6Ladder()
    {
        Assert.Equal(0f, StatCalculator.PetGradedImBonus(76500, AmuletSort, Pack(0, 0, 60), statType: 6));
    }

    // ---- Amulet flat attack / defense tables (sort 28, GameSystem_07_Pet.cpp:856-910) ----

    [Theory]
    [InlineData(8290, 275)]
    [InlineData(76000, 3000)]
    [InlineData(76004, 3000)]
    [InlineData(76005, 3000)]
    [InlineData(76006, 4000)]
    [InlineData(76007, 5000)]
    public void AmuletAttackBonus_MatchesConfirmedTable(int itemId, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetAmuletAttackBonus(itemId, AmuletSort));
    }

    [Theory]
    [InlineData(8290, 550)]
    [InlineData(76000, 3000)]
    [InlineData(76004, 3000)]
    [InlineData(76005, 5000)]
    [InlineData(76006, 7500)]
    [InlineData(76007, 12500)]
    public void AmuletDefenseBonus_MatchesConfirmedTable(int itemId, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetAmuletDefenseBonus(itemId, AmuletSort));
    }

    [Fact]
    public void AmuletFlatTables_ZeroForNonAmuletSort()
    {
        Assert.Equal(0, StatCalculator.PetAmuletAttackBonus(76005, GrowPetSort));
        Assert.Equal(0, StatCalculator.PetAmuletDefenseBonus(76005, GrowPetSort));
    }

    [Fact]
    public void AmuletFlatTables_ZeroForUntranscribedId()
    {
        // A qualifying-but-not-transcribed id -> 0 ("not yet transcribed"), not proof of a legacy 0.
        Assert.Equal(0, StatCalculator.PetAmuletAttackBonus(2200, AmuletSort));
        Assert.Equal(0, StatCalculator.PetAmuletDefenseBonus(2200, AmuletSort));
    }

    // ---- Grow-percent (GameSystem_07_Pet.cpp:437-530): 100 at tier max, 200 at twice tier max ----

    [Fact]
    public void GrowPercent_IsExactly100AtTierMaximum()
    {
        Assert.Equal(100f, StatCalculator.PetGrowPercent(Tier0Max, Tier0Max));
    }

    [Fact]
    public void GrowPercent_Is200AtTwiceTierMaximum_TheFullyEvolvedCeiling()
    {
        Assert.Equal(200f, StatCalculator.PetGrowPercent(Tier0Max * 2, Tier0Max));
    }

    [Fact]
    public void GrowPercent_ZeroForBelowOneGrow()
    {
        Assert.Equal(0f, StatCalculator.PetGrowPercent(0, Tier0Max));
    }

    [Fact]
    public void GrowPercent_ZeroForUnrecognisedIndex()
    {
        // tierMax <= 0 signals an unrecognised index.
        Assert.Equal(0f, StatCalculator.PetGrowPercent(Tier0Max, tierMax: 0));
    }

    // ---- Stepped attack bonus (GameSystem_07_Pet.cpp:1722-1802): 101/125/150/175/200 -> ...250 ----

    [Theory]
    // grow -> percent -> expected step (active pet)
    [InlineData(0, 0)] // 0% -> below 101 -> 0
    [InlineData(1_000_000, 0)] // 2.5% -> 0
    [InlineData(41_000_000, 50)] // 102.5% -> 50
    [InlineData(50_000_000, 100)] // 125% -> 100
    [InlineData(60_000_000, 150)] // 150% -> 150
    [InlineData(70_000_000, 200)] // 175% -> 200
    [InlineData(80_000_000, 250)] // 200% -> 250
    [InlineData(120_000_000, 250)] // 300% -> 250 (at/above 200%)
    public void SteppedAttackBonus_StepsOnGrowthPercent(int growValue, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetSteppedAttackBonus(growValue, Tier0Max, activity: 1));
    }

    [Fact]
    public void SteppedAttackBonus_ZeroWhenInactive()
    {
        // Fully evolved but inactive -> attack contribution gated off (activity < 1).
        Assert.Equal(0, StatCalculator.PetSteppedAttackBonus(80_000_000, Tier0Max, activity: 0));
    }

    [Fact]
    public void SteppedAttackBonus_ZeroForUnrecognisedIndex()
    {
        Assert.Equal(0, StatCalculator.PetSteppedAttackBonus(80_000_000, tierMax: 0, activity: 1));
    }

    // ---- Graded growth-value decoders (two live paths, GameSystem_07_Pet.cpp:976-1062) ----

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    public void GrowthValueAttackGrade_DecodesIsByte(int isByte, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetGrowthValueAttackGrade(Pack(isByte)));
    }

    [Theory]
    [InlineData(11, 1)]
    [InlineData(12, 2)]
    [InlineData(13, 3)]
    [InlineData(10, 0)]
    [InlineData(14, 0)]
    public void GrowthValueBonusSkillGrade_DecodesIuByte(int iuByte, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetGrowthValueBonusSkillGrade(Pack(0, iuByte)));
    }

    // ---- Bonus-skill index -> IU stat type (MyFactor.cpp:4550-4572) ----

    [Theory]
    [InlineData(103, 1)]
    [InlineData(82, 2)]
    [InlineData(83, 3)]
    [InlineData(105, 4)]
    [InlineData(104, 5)]
    [InlineData(84, 6)]
    [InlineData(999, 0)]
    public void BonusSkillStatType_MapsSkillIndexToType(int skillIndex, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetBonusSkillStatType(skillIndex));
    }

    // ---- end-to-end wiring guard (2026-07-11 confirmation pass: flat amulet attack/defense tables now wired
    // into ComputeBaseStats for the 6 non-overlapping ids; the 3 Phoenix-overlapping ids stay excluded) ----

    private static readonly CharacterBaseAttributes NeutralAttributes = new(
        Vitality: 0, Strength: 0, Intelligence: 0, Dexterity: 0,
        Level: 1, Tribe: 0, PreviousTribe: 0, Title: 0, Halo: 0, RebirthCount: 0);

    private static readonly FrozenDictionary<short, LevelRowDto> NeutralLevels =
        new Dictionary<short, LevelRowDto> { [1] = new LevelRowDto(1, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0) }
            .ToFrozenDictionary();

    // Minimal amulet ItemRowDto: only ItemId/Sort matter for the flat tables/Phoenix switch, and AttackPower/
    // DefensePower are pinned to 0 so the slot's own "subtract the item's stat" term stays a no-op, isolating
    // the getter contribution under test.
    private static ItemRowDto AmuletItem(int itemId)
    {
        return new ItemRowDto(
            itemId, $"Amulet{itemId}", null, null, null,
            0, AmuletSort, 0, 0, 0,
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

    private static EquippedItemSlot[] AmuletEquipment(int itemId)
    {
        return [new EquippedItemSlot(8, AmuletItem(itemId), 0, 0, 0, 0)];
    }

    [Theory]
    // 8290 and the 76000-76004 rows have no overlap with any Phoenix term -- the full table value must land.
    [InlineData(8290, 275, 550)]
    [InlineData(76000, 3000, 3000)]
    [InlineData(76004, 3000, 3000)]
    public void AmuletFlatBonus_NonOverlappingIds_WiredIntoComputeBaseStats(int itemId, int expectedAttackDelta,
        int expectedDefenseDelta)
    {
        var baseline = StatCalculator.ComputeBaseStats(NeutralAttributes, [], NeutralLevels);
        var withAmulet = StatCalculator.ComputeBaseStats(NeutralAttributes, AmuletEquipment(itemId), NeutralLevels);

        Assert.Equal(baseline.AttackPower + expectedAttackDelta, withAmulet.AttackPower);
        Assert.Equal(baseline.DefensePower + expectedDefenseDelta, withAmulet.DefensePower);
    }

    [Theory]
    // 76005/76006/76007: the flat table's own entry must NOT be added on top of the two existing PhoenixFlatBonus
    // passes already summed in ComputeAttackPower/ComputeDefensePower -- only the Phoenix total should show up.
    // Attack Phoenix total = first pass (3000/4000/5000) + PhoenixDamageSecondPassBonus (0/1000/2000) = 3000/5000/7000.
    // Defense Phoenix total = first pass (5000/7500/12500) + final pass (2000/4500/9500) = 7000/12000/22000.
    [InlineData(76005, 3000, 7000)]
    [InlineData(76006, 5000, 12000)]
    [InlineData(76007, 7000, 22000)]
    public void AmuletFlatBonus_PhoenixOverlapIds_NotDoubleCountedInComputeBaseStats(int itemId,
        int expectedAttackDelta, int expectedDefenseDelta)
    {
        var baseline = StatCalculator.ComputeBaseStats(NeutralAttributes, [], NeutralLevels);
        var withAmulet = StatCalculator.ComputeBaseStats(NeutralAttributes, AmuletEquipment(itemId), NeutralLevels);

        Assert.Equal(baseline.AttackPower + expectedAttackDelta, withAmulet.AttackPower);
        Assert.Equal(baseline.DefensePower + expectedDefenseDelta, withAmulet.DefensePower);
    }
}
