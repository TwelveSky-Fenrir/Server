using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B3-deco -- the per-slot equipment stat ramps (<c>MyUtil::ReturnIUEffectValue</c>,
///     Server/ts25zone/S07_MyGame03.cpp:1134-1341) and the decoration "sort==2" octet tables
///     (<c>ITEMSYSTEM::ReturnNewValue</c>/<c>ReturnNewStat</c>,
///     Server/ts25zone/GameSystem/GameSystem_02_Item.cpp:1215-1368). Pure value functions: these tests pin the
///     shared level ramp, each (effect sort, category) base/K row, the disjoint octet bands, the two decoration
///     tables, the signed-octet wrap, and the octet-to-table split -- no wiring, no equipment fixtures.
/// </summary>
public class DecoUpgradeContributionTests
{
    // ============================================================================================
    //  ReturnIUEffectValue -- shared level ramp + (effect sort, category) base/K
    // ============================================================================================

    // Effect sort 1 (weapon attack) -- the contract's own worked reference, and the anchor that this general
    // engine reproduces the same ramp as the standalone WeaponAttackEffectValue in StatCalculator.AttackPower.cs.
    [Theory]
    [InlineData(99, 18)]
    [InlineData(100, 18)]
    [InlineData(112, 20)]
    [InlineData(113, 20)]
    [InlineData(145, 31)]
    [InlineData(146, 0)] // level >= 146: no band matches
    [InlineData(200, 0)]
    public void ReturnIUEffectValue_Sort1_WorkedReference(int itemLevel, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnIUEffectValue(1, 4, itemLevel));
    }

    [Fact]
    public void ReturnIUEffectValue_Sort1_EligibleCategories()
    {
        // Category 4 and 13-21 are eligible for sort 1; all share the same base/K, so all give 18 at level 100.
        Assert.Equal(18, StatCalculator.ReturnIUEffectValue(1, 4, 100));
        for (var category = 13; category <= 21; category++)
            Assert.Equal(18, StatCalculator.ReturnIUEffectValue(1, category, 100));
    }

    // Every sort 2-6 (base, K) row, evaluated at level 100 (r == 6.0) so the expected truncation is easy to check.
    [Theory]
    [InlineData(2, 8, 2)] // base 2.00, K 0.10 -> (int)(2.00 + 6*0.10) = 2
    [InlineData(2, 9, 8)] // base 6.36, K 0.32 -> (int)(6.36 + 6*0.32) = 8
    [InlineData(2, 10, 2)] // base 1.82, K 0.09 -> (int)(1.82 + 6*0.09) = 2
    [InlineData(2, 12, 1)] // base 0.91, K 0.05 -> (int)(0.91 + 6*0.05) = 1
    [InlineData(3, 10, 17)] // base 13.36, K 0.67 -> (int)(13.36 + 6*0.67) = 17
    [InlineData(3, 13, 7)] // base 5.73, K 0.29 -> (int)(5.73 + 6*0.29) = 7
    [InlineData(3, 21, 7)] // 13-21 share the same row
    [InlineData(4, 9, 1)] // base 0.95, K 0.05 -> (int)(0.95 + 6*0.05) = 1
    [InlineData(4, 12, 2)] // base 2.23, K 0.11 -> (int)(2.23 + 6*0.11) = 2
    [InlineData(5, 11, 3)] // base 2.00, K 0.26 -> (int)(2.00 + 6*0.26) = 3
    [InlineData(6, 7, 1)] // base 1.00, K 0.13 -> (int)(1.00 + 6*0.13) = 1
    public void ReturnIUEffectValue_Sorts2To6_BaseKRows(int effectSort, int category, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnIUEffectValue(effectSort, category, 100));
    }

    [Theory]
    [InlineData(2, 7)] // sort 2 has no branch for category 7
    [InlineData(2, 11)]
    [InlineData(3, 9)] // sort 3 category 10 and 13-21 only; 9/11/12 ineligible
    [InlineData(3, 12)]
    [InlineData(4, 8)] // sort 4 category 9/12 only
    [InlineData(4, 10)]
    [InlineData(5, 10)] // sort 5 category 11 only
    [InlineData(5, 12)]
    [InlineData(6, 8)] // sort 6 category 7 only
    [InlineData(6, 11)]
    public void ReturnIUEffectValue_IneligibleCategory_IsZero(int effectSort, int category)
    {
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(effectSort, category, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(99)]
    public void ReturnIUEffectValue_EffectSortOutside1To6_IsZero(int effectSort)
    {
        // A category that is eligible for SOME sort still yields 0 when the effect sort itself has no table.
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(effectSort, 10, 100));
    }

    [Fact]
    public void ReturnIUEffectValue_Level146AndAbove_IsZero_EverySort()
    {
        // Category chosen to be eligible for each sort so only the level guard can zero it.
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(1, 4, 146));
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(2, 9, 146));
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(3, 10, 200));
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(4, 12, 150));
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(5, 11, 146));
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(6, 7, 999));
    }

    [Fact]
    public void ReturnIUEffectValue_BelowLevel45_NoClampAtBase()
    {
        // level 0: r = (0-45)*0.10 = -4.5; sort 1 -> (int)(14.34 + (-4.5)*0.72) = (int)11.1 = 11.
        // The value drops BELOW base (14) rather than flooring at base or 0 -- a reproduced quirk.
        Assert.Equal(11, StatCalculator.ReturnIUEffectValue(1, 4, 0));
    }

    [Theory]
    [InlineData(5, 90)] // 18 per point * 5 IU
    [InlineData(0, 0)]
    [InlineData(-3, -54)] // negative IU point count -> negative product (reproduced, no clamp)
    public void IUEffectSlotContribution_MultipliesPerPointByCount(int iuPointCount, int expected)
    {
        // ReturnIUEffectValue(1, 4, 100) == 18.
        Assert.Equal(expected, StatCalculator.IUEffectSlotContribution(1, 4, 100, iuPointCount));
    }

    [Fact]
    public void IUEffectSlotContribution_IneligibleItem_IsZeroRegardlessOfCount()
    {
        Assert.Equal(0, StatCalculator.IUEffectSlotContribution(2, 7, 100, 99));
    }

    // ============================================================================================
    //  Decoration tSort=1 -- disjoint-band table (low octets IS/IU/IM)
    // ============================================================================================

    [Theory]
    // MaxLife: band 41-60 -> 100*(octet-40)
    [InlineData(DecorationStatKind.MaxLife, 41, 100)]
    [InlineData(DecorationStatKind.MaxLife, 60, 2000)]
    [InlineData(DecorationStatKind.MaxLife, 40, 0)]
    [InlineData(DecorationStatKind.MaxLife, 61, 0)]
    // MaxMana: band 61-80 -> 125*(octet-60)
    [InlineData(DecorationStatKind.MaxMana, 61, 125)]
    [InlineData(DecorationStatKind.MaxMana, 80, 2500)]
    [InlineData(DecorationStatKind.MaxMana, 60, 0)]
    [InlineData(DecorationStatKind.MaxMana, 81, 0)]
    // DefensePower: band 1-20 -> 50*octet
    [InlineData(DecorationStatKind.DefensePower, 1, 50)]
    [InlineData(DecorationStatKind.DefensePower, 20, 1000)]
    [InlineData(DecorationStatKind.DefensePower, 0, 0)]
    [InlineData(DecorationStatKind.DefensePower, 21, 0)]
    // AttackBlock: band 21-40 -> 20*(octet-20)
    [InlineData(DecorationStatKind.AttackBlock, 21, 20)]
    [InlineData(DecorationStatKind.AttackBlock, 40, 400)]
    [InlineData(DecorationStatKind.AttackBlock, 20, 0)]
    [InlineData(DecorationStatKind.AttackBlock, 41, 0)]
    // ElementDefensePower: band 81-100 -> 50*(octet-80)
    [InlineData(DecorationStatKind.ElementDefensePower, 81, 50)]
    [InlineData(DecorationStatKind.ElementDefensePower, 100, 1000)]
    [InlineData(DecorationStatKind.ElementDefensePower, 80, 0)]
    [InlineData(DecorationStatKind.ElementDefensePower, 101, 0)]
    public void ReturnNewValue_LowOctet_DisjointBands(DecorationStatKind stat, int octet, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnNewValue(1, stat, octet));
    }

    [Theory]
    [InlineData(45)]
    [InlineData(0)]
    [InlineData(100)]
    public void ReturnNewValue_LowOctet_AttackPowerSelector_AlwaysZero(int octet)
    {
        // Selector 3 (AttackPower) has no branch in either table -> total no-op.
        Assert.Equal(0, StatCalculator.ReturnNewValue(1, DecorationStatKind.AttackPower, octet));
    }

    // ============================================================================================
    //  Decoration tSort=2 -- the decoration ramp (high octet IZ)
    // ============================================================================================

    [Theory]
    // Tiered by 5, band REPEATING at 26.
    [InlineData(1, 400)]
    [InlineData(5, 400)]
    [InlineData(26, 400)]
    [InlineData(30, 400)]
    [InlineData(6, 800)]
    [InlineData(35, 800)]
    [InlineData(11, 1200)]
    [InlineData(40, 1200)]
    [InlineData(16, 1600)]
    [InlineData(45, 1600)]
    [InlineData(21, 2000)]
    [InlineData(50, 2000)]
    [InlineData(0, 0)]
    [InlineData(51, 0)]
    [InlineData(-5, 0)]
    public void ReturnNewValue_HighOctet_MaxLife_TieredRepeatingAt26(int octet, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnNewValue(2, DecorationStatKind.MaxLife, octet));
    }

    [Theory]
    // DefensePower: octet must be in 1-25, then keyed only on octet-modulo-5.
    [InlineData(1, 200)] // 1 % 5 = 1
    [InlineData(2, 400)]
    [InlineData(3, 600)]
    [InlineData(4, 800)]
    [InlineData(5, 1000)] // 5 % 5 = 0
    [InlineData(25, 1000)]
    [InlineData(26, 0)] // outside 1-25
    [InlineData(0, 0)]
    public void ReturnNewValue_HighOctet_DefensePower_ModuloFiveGated1To25(int octet, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnNewValue(2, DecorationStatKind.DefensePower, octet));
    }

    [Theory]
    // ElementDefensePower: identical modulo-5 map but gated to 26-50.
    [InlineData(26, 200)] // 26 % 5 = 1
    [InlineData(27, 400)]
    [InlineData(30, 1000)] // 30 % 5 = 0
    [InlineData(50, 1000)]
    [InlineData(25, 0)] // outside 26-50
    [InlineData(51, 0)]
    public void ReturnNewValue_HighOctet_ElementDefense_ModuloFiveGated26To50(int octet, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnNewValue(2, DecorationStatKind.ElementDefensePower, octet));
    }

    [Theory]
    [InlineData(DecorationStatKind.MaxMana)] // unconditional 0
    [InlineData(DecorationStatKind.AttackBlock)] // unconditional 0
    [InlineData(DecorationStatKind.AttackPower)] // no branch
    public void ReturnNewValue_HighOctet_AlwaysZeroSelectors(DecorationStatKind stat)
    {
        for (var octet = 0; octet <= 60; octet++)
            Assert.Equal(0, StatCalculator.ReturnNewValue(2, stat, octet));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void ReturnNewValue_UnknownTableSort_IsZero(int tableSort)
    {
        Assert.Equal(0, StatCalculator.ReturnNewValue(tableSort, DecorationStatKind.MaxLife, 45));
    }

    [Theory]
    [InlineData(5)] // no selector 5 branch in either table
    [InlineData(7)] // no selector 7 branch in either table
    public void ReturnNewValue_SelectorWithNoBranch_IsZero(int selector)
    {
        var stat = (DecorationStatKind)selector;
        for (var octet = 0; octet <= 100; octet++)
        {
            Assert.Equal(0, StatCalculator.ReturnNewValue(1, stat, octet));
            Assert.Equal(0, StatCalculator.ReturnNewValue(2, stat, octet));
        }
    }

    // ============================================================================================
    //  ReturnNewStat -- four-octet sum, octet-to-table split, signed wrap, zero short-circuit
    // ============================================================================================

    [Fact]
    public void ReturnNewStat_ZeroPackedValue_ShortCircuitsToZero()
    {
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.MaxLife, 0));
    }

    [Fact]
    public void ReturnNewStat_SumsThreeLowOctetsViaTSort1_AndHighOctetViaTSort2()
    {
        // MaxLife: IS=45 (tSort1 -> 100*(45-40)=500), IU=0, IM=0, IZ=3 (tSort2 tier 1-5 -> 400) = 900.
        var packed = StatCalculator.PackDecorationUpgradeOctets(45, 0, 0, 3);
        Assert.Equal(900, StatCalculator.ReturnNewStat(DecorationStatKind.MaxLife, packed));
    }

    [Fact]
    public void ReturnNewStat_AllThreeLowOctetsContributeIndependently()
    {
        // MaxLife band 41-60 for each low octet: IS=50 -> 1000, IU=55 -> 1500, IM=41 -> 100; IZ=48 -> tier 2000.
        var packed = StatCalculator.PackDecorationUpgradeOctets(50, 55, 41, 48);
        Assert.Equal(1000 + 1500 + 100 + 2000, StatCalculator.ReturnNewStat(DecorationStatKind.MaxLife, packed));
    }

    [Fact]
    public void ReturnNewStat_LowOctetRoutesToTSort1_HighOctetRoutesToTSort2()
    {
        // DefensePower: only IS set (=3) -> tSort1 50*3 = 150; IZ=0 -> nothing.
        var lowOnly = StatCalculator.PackDecorationUpgradeOctets(3, 0, 0, 0);
        Assert.Equal(150, StatCalculator.ReturnNewStat(DecorationStatKind.DefensePower, lowOnly));

        // DefensePower: only IZ set (=3) -> tSort2 (3 in 1-25, 3%5=3) 600; low octets 0 -> nothing.
        var highOnly = StatCalculator.PackDecorationUpgradeOctets(0, 0, 0, 3);
        Assert.Equal(600, StatCalculator.ReturnNewStat(DecorationStatKind.DefensePower, highOnly));
    }

    [Fact]
    public void ReturnNewStat_SignedOctetWrap_StoredByteAbove127_ContributesZero()
    {
        // A stored byte of 200 in the IS position decodes as signed -56, outside every band -> 0. The packed
        // value is non-zero so it does NOT short-circuit; the zero comes from the signed-band decode.
        var packed = StatCalculator.PackDecorationUpgradeOctets(200, 0, 0, 0);
        Assert.NotEqual(0, packed);
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.DefensePower, packed));
    }

    [Fact]
    public void ReturnNewStat_MaxMana_IzOctetNeverContributes()
    {
        // MaxMana IZ (tSort2) is unconditional 0: a packed value carrying ONLY an IZ octet gives 0 for MaxMana,
        // even though the same IZ octet would score for MaxLife/DefensePower.
        var packed = StatCalculator.PackDecorationUpgradeOctets(0, 0, 0, 3);
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.MaxMana, packed));

        // But the IU low octet (in the 61-80 MaxMana band) still contributes via tSort1.
        var withLowOctet = StatCalculator.PackDecorationUpgradeOctets(0, 61, 0, 3);
        Assert.Equal(125, StatCalculator.ReturnNewStat(DecorationStatKind.MaxMana, withLowOctet));
    }

    [Fact]
    public void ReturnNewStat_AttackPowerSelector_AlwaysZero()
    {
        // Decoration attack-power channel is a total no-op regardless of octets.
        var packed = StatCalculator.PackDecorationUpgradeOctets(45, 55, 15, 3);
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.AttackPower, packed));
    }

    // ============================================================================================
    //  PackDecorationUpgradeOctets -- explicit octet order (IS lowest, IZ highest)
    // ============================================================================================

    [Fact]
    public void PackDecorationUpgradeOctets_LaysOutOctetsLeastSignificantFirst()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(0x11, 0x22, 0x33, 0x44);
        Assert.Equal(0x44332211, packed);
    }

    [Fact]
    public void PackDecorationUpgradeOctets_NegativeOctet_WrapsToStoredByte()
    {
        // -56 stores as byte 200 in the low octet.
        var packed = StatCalculator.PackDecorationUpgradeOctets(-56, 0, 0, 0);
        Assert.Equal(200, packed & 0xFF);
    }

    // ============================================================================================
    //  IsDecorationItem -- the "deco sort==2" classification gate
    // ============================================================================================

    [Theory]
    [InlineData(5, 11, true)]
    [InlineData(5, 12, true)]
    [InlineData(5, 13, true)]
    [InlineData(5, 14, true)]
    [InlineData(5, 10, false)] // category outside 11-14
    [InlineData(5, 15, false)]
    [InlineData(4, 11, false)] // item type not 5
    [InlineData(6, 12, false)]
    [InlineData(0, 0, false)]
    public void IsDecorationItem_Type5AndCategory11To14(int itemType, int equipInfoCategory, bool expected)
    {
        Assert.Equal(expected, StatCalculator.IsDecorationItem(itemType, equipInfoCategory));
    }
}
