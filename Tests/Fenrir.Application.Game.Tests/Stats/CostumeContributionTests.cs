using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B6 -- costume value + costume-enchant BASE-stat contributions
///     (<c>Server/Header/Protocol/MyFactor.cpp:356-357,1679-3670</c>, <c>Server/Header/function.h:1798-2163</c>).
///     This exercises the pure decode/contribution math directly rather than through
///     <c>StatCalculator.ComputeBaseStats</c>/<c>ComputeEffectiveStats</c> -- both the getter wiring (Vitality/
///     Strength/Ki/Wisdom/Critical/Luck each call these helpers) and the end-to-end
///     <c>PlayerRuntimeState.CostumeIndex</c>/<c>CostumeDate</c> -&gt; <c>CosmeticContext.CostumeEnchantCs</c>
///     source-field wiring are live (the latter added by the follow-up wave14/B6 workstream); see
///     <c>CostumeEnchantWiringTests</c> for that end-to-end coverage instead.
/// </summary>
public class CostumeContributionTests
{
    // ---- Valid-costume id set (function.h:1798-2008) ----

    [Theory]
    [InlineData(301)] // bottom of 301-402
    [InlineData(402)] // top of 301-402
    [InlineData(2146)]
    [InlineData(2148)]
    [InlineData(1801)]
    [InlineData(1893)]
    [InlineData(17701)]
    [InlineData(17703)]
    [InlineData(18124)]
    [InlineData(18132)]
    [InlineData(93301)]
    [InlineData(93316)] // commented out at first appearance, RE-ADDED (:1997-1998) -> valid
    [InlineData(93317)] // ditto
    [InlineData(93330)] // top of 93318-93330
    [InlineData(93334)] // bottom of 93334-93345
    [InlineData(93381)] // top of 93376-93381
    [InlineData(93385)] // bottom of 93385-93387
    [InlineData(93405)] // top of 93388-93405
    [InlineData(76524)]
    [InlineData(76526)]
    public void ValidCostume_IncludesEnumeratedIds(int costumeId)
    {
        Assert.True(StatCalculator.IsValidCostume(costumeId));
        Assert.Contains(costumeId, StatCalculator.ValidCostumeIds);
    }

    [Theory]
    [InlineData(300)] // just below 301-402
    [InlineData(403)] // just above 301-402
    [InlineData(93331)] // commented out -> NOT valid
    [InlineData(93332)] // commented out -> NOT valid
    [InlineData(93333)] // gap between 93330 and 93334
    [InlineData(93382)] // commented out -> NOT valid
    [InlineData(93383)]
    [InlineData(93384)]
    [InlineData(76523)]
    [InlineData(76527)]
    [InlineData(0)]
    public void ValidCostume_ExcludesCommentedOutAndGapIds(int costumeId)
    {
        Assert.False(StatCalculator.IsValidCostume(costumeId));
        Assert.DoesNotContain(costumeId, StatCalculator.ValidCostumeIds);
    }

    // ---- Deco-stat set reuse (function.h:2030-2072, same legacy list as IsCustomDecoStatItem) ----

    [Theory]
    [InlineData(101)] // bottom of 101-151 range
    [InlineData(151)] // top of 101-151 range
    [InlineData(594)]
    [InlineData(1385)]
    [InlineData(1483)]
    [InlineData(2307)]
    [InlineData(8012)]
    [InlineData(91483)]
    [InlineData(91488)]
    public void DecoStatCostume_IncludesLegacyDecoList(int costumeId)
    {
        Assert.True(StatCalculator.IsDecoStatCostume(costumeId));
    }

    [Theory]
    [InlineData(100)] // just below 101
    [InlineData(152)] // just above 151
    [InlineData(593)]
    [InlineData(91489)]
    [InlineData(0)]
    public void DecoStatCostume_ExcludesNonDecoIds(int costumeId)
    {
        Assert.False(StatCalculator.IsDecoStatCostume(costumeId));
    }

    [Fact]
    public void DecoAndValidSets_AreDisjoint()
    {
        // Every valid-costume id must NOT be a deco item, and vice-versa (contract edge case 4).
        foreach (var id in StatCalculator.ValidCostumeIds)
            Assert.False(StatCalculator.IsDecoStatCostume(id));

        int[] decoIds = [101, 151, 594, 595, 596, 1385, 1389, 1393, 1483, 1484, 1485, 2307, 2308, 2309, 8010, 8011, 8012, 91483, 91488];
        foreach (var id in decoIds)
            Assert.False(StatCalculator.IsValidCostume(id));
    }

    // ---- aCostumeValue: the four cached base costume stats (function.h:2074-2106) ----

    [Fact]
    public void CostumeBaseStatBlock_ItemNotFound_IsAllZero()
    {
        // Even with populated raw stats, item-not-found yields an all-zero block (edge case 1).
        var block = StatCalculator.ComputeCostumeBaseStatBlock(301, itemFound: false, 50, 60, 70, 80);
        Assert.Equal(default(CostumeBaseStatBlock), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_PlainItem_CopiesRawStats()
    {
        // A found item in neither set: raw stats pass through unchanged, no clamp, no bonus.
        var block = StatCalculator.ComputeCostumeBaseStatBlock(500, itemFound: true, 5, 6, 7, 8);
        Assert.Equal(new CostumeBaseStatBlock(5, 6, 7, 8), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_ValidCostume_AddsFlat100ToEachStat()
    {
        var block = StatCalculator.ComputeCostumeBaseStatBlock(301, itemFound: true, 10, 20, 30, 40);
        Assert.Equal(new CostumeBaseStatBlock(110, 120, 130, 140), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_DecoItem_ClampsEachStatUpTo100()
    {
        // All four raw stats below 100 -> each raised to exactly 100 (edge case 2).
        var block = StatCalculator.ComputeCostumeBaseStatBlock(101, itemFound: true, 10, 20, 30, 40);
        Assert.Equal(new CostumeBaseStatBlock(100, 100, 100, 100), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_DecoItem_LeavesStatsAtOrAbove100Unchanged()
    {
        // Only sub-100 stats are raised; >=100 stats are left as-is (edge case 2).
        var block = StatCalculator.ComputeCostumeBaseStatBlock(151, itemFound: true, 150, 20, 200, 99);
        Assert.Equal(new CostumeBaseStatBlock(150, 100, 200, 100), block);
    }

    // ---- aCostumeEnchantValue.cs: signed low-byte decode + index gate (function.h:2141-2163, :317-322) ----

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(96, 96)] // the Critical sentinel value, still an ordinary positive low byte here
    [InlineData(127, 127)] // top of the positive signed-byte range
    [InlineData(128, -128)] // >=128 reads back negative through the signed byte (function.h:317-322)
    [InlineData(200, -56)]
    [InlineData(255, -1)]
    [InlineData(256, 0)] // only the low byte matters; higher bytes are ignored
    [InlineData(0x0160, 96)] // low byte 0x60 = 96; high byte ignored
    public void ReadSignedLowByte_InterpretsLowByteAsSignedChar(int packedWord, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReadSignedLowByte(packedWord));
    }

    [Theory]
    [InlineData(9)] // just below the gate
    [InlineData(20)] // just above the gate
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void DecodeCostumeEnchantCs_IndexOutsideGate_IsZero(int costumeIndex)
    {
        // Ten slots each carrying a distinct non-zero low byte; the gate alone must zero the result.
        ReadOnlySpan<int> date = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        Assert.Equal(0, StatCalculator.DecodeCostumeEnchantCs(costumeIndex, date));
    }

    [Theory]
    [InlineData(10, 0, 11)] // index 10 -> slot 0
    [InlineData(11, 1, 22)]
    [InlineData(15, 5, 66)]
    [InlineData(19, 9, 110)] // index 19 -> slot 9
    public void DecodeCostumeEnchantCs_SelectsSlotByIndexModuloTen(int costumeIndex, int expectedSlot, int lowByte)
    {
        // date[slot] carries lowByte; every other slot 0 -> proves the modulo-ten slot selection.
        var raw = new int[10];
        raw[expectedSlot] = lowByte;
        ReadOnlySpan<int> date = raw;

        Assert.Equal(lowByte, StatCalculator.DecodeCostumeEnchantCs(costumeIndex, date));
    }

    [Fact]
    public void DecodeCostumeEnchantCs_NegativeLowByte_ReadsNegative()
    {
        // Slot 3 (index 13) carries a low byte of 200 -> reads back -56 (edge case 7).
        var raw = new int[10];
        raw[3] = 200;
        ReadOnlySpan<int> date = raw;

        Assert.Equal(-56, StatCalculator.DecodeCostumeEnchantCs(13, date));
    }

    [Fact]
    public void DecodeCostumeEnchantCs_ShortSpan_IsZeroDefensively()
    {
        // Fewer than ten slots supplied: the selected slot is out of range -> defensive zero.
        ReadOnlySpan<int> date = [11, 22];
        Assert.Equal(0, StatCalculator.DecodeCostumeEnchantCs(15, date)); // slot 5 not present
    }

    // ---- Per-getter contribution helpers (MyFactor.cpp) ----

    [Fact]
    public void VitKiStrWisContributions_AddCsWithoutPositiveGuard()
    {
        var block = new CostumeBaseStatBlock(Vitality: 110, Strength: 120, Intelligence: 130, Dexterity: 140);

        // Positive cs: added straight.
        Assert.Equal(115, StatCalculator.CostumeVitalityContribution(block, 5));
        Assert.Equal(135, StatCalculator.CostumeKiContribution(block, 5)); // Ki reads Intelligence
        Assert.Equal(125, StatCalculator.CostumeStrengthContribution(block, 5));
        Assert.Equal(145, StatCalculator.CostumeWisdomContribution(block, 5)); // Wisdom reads Dexterity

        // Negative cs: subtracts (no positive guard, edge case 7).
        Assert.Equal(54, StatCalculator.CostumeVitalityContribution(block, -56));
        Assert.Equal(74, StatCalculator.CostumeKiContribution(block, -56));
        Assert.Equal(64, StatCalculator.CostumeStrengthContribution(block, -56));
        Assert.Equal(84, StatCalculator.CostumeWisdomContribution(block, -56));
    }

    [Fact]
    public void KiContribution_ReadsIntelligence_WisdomContribution_ReadsDexterity()
    {
        // Distinct field values so a wrong-field mapping would be caught.
        var block = new CostumeBaseStatBlock(Vitality: 1, Strength: 2, Intelligence: 3, Dexterity: 4);

        Assert.Equal(3, StatCalculator.CostumeKiContribution(block, 0)); // Int, not a Ki field
        Assert.Equal(4, StatCalculator.CostumeWisdomContribution(block, 0)); // Dex, not a Wisdom field
        Assert.Equal(1, StatCalculator.CostumeVitalityContribution(block, 0));
        Assert.Equal(2, StatCalculator.CostumeStrengthContribution(block, 0));
    }

    [Theory]
    [InlineData(96, 10)] // the sentinel: exactly 96 -> flat +10 (NOT 9)
    [InlineData(95, 9)] // ordinary positive -> magnitude/10
    [InlineData(97, 9)]
    [InlineData(100, 10)]
    [InlineData(90, 9)]
    [InlineData(9, 0)] // integer division drops the remainder
    [InlineData(1, 0)]
    [InlineData(0, 0)] // zero or below -> nothing
    [InlineData(-5, 0)]
    [InlineData(-128, 0)]
    public void CriticalContribution_GuardsPositive_WithNinetySixSentinel(int cs, int expected)
    {
        Assert.Equal(expected, StatCalculator.CostumeCriticalContribution(cs));
    }

    // ---- 96-sentinel rationale, recovered by workstream costume-contribution-magnitude96 ----
    // (MyFactor.cpp:3409-3414 unchanged; this documents/asserts WHY 96 is special, not a new rule.)

    [Fact]
    public void CriticalContribution_NinetySixSentinel_MatchesCostumeEnchantHardCap()
    {
        // 96 is not an arbitrary Critical-formula constant: it is the literal ceiling the costume-enchant
        // magnitude can ever reach (CostumeImproveResolver rejects any attempt at/above this value,
        // Server/ts25zone/S04_MyWork02.cpp:2616-2623) -- the two constants must stay in lockstep.
        Assert.Equal(96, CostumeImproveResolver.MaxCostumeImprove);
    }

    [Fact]
    public void CriticalContribution_AtCap_IsDistinguishableFromTheNearMaxRange()
    {
        // Plain truncating division by 10 collapses every magnitude 90-95 to the same value (9) a
        // genuinely-maxed 96 would otherwise also produce -- the +10 special case is what keeps a
        // fully-maxed enchant numerically distinguishable from a merely-near-max one.
        for (var cs = 90; cs <= 95; cs++)
            Assert.Equal(9, StatCalculator.CostumeCriticalContribution(cs));

        Assert.Equal(10, StatCalculator.CostumeCriticalContribution(96));
        Assert.NotEqual(95 / 10, StatCalculator.CostumeCriticalContribution(96));
    }

    [Fact]
    public void LuckContribution_AddsCsTimesTwo_PlusFlat100ForValidCostume()
    {
        // Valid worn id + positive cs: cs*2 + 100.
        Assert.Equal(120, StatCalculator.CostumeLuckContribution(301, 10));

        // Non-valid worn id + positive cs: cs*2 only.
        Assert.Equal(20, StatCalculator.CostumeLuckContribution(500, 10));

        // Valid worn id + zero cs: the +100 fires independently of the enchant.
        Assert.Equal(100, StatCalculator.CostumeLuckContribution(301, 0));

        // Valid worn id + negative cs: cs*2 subtracts, +100 still applies (no positive guard).
        Assert.Equal(90, StatCalculator.CostumeLuckContribution(301, -5));

        // Non-valid worn id + negative cs: cs*2 only, subtracts.
        Assert.Equal(-10, StatCalculator.CostumeLuckContribution(500, -5));
    }
}
