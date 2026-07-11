using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Stats;

public class DecoUpgradeContributionTests
{

    [Theory]
    [InlineData(99, 18)]
    [InlineData(100, 18)]
    [InlineData(112, 20)]
    [InlineData(113, 20)]
    [InlineData(145, 31)]
    [InlineData(146, 0)]
    [InlineData(200, 0)]
    public void ReturnIUEffectValue_Sort1_WorkedReference(int itemLevel, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnIUEffectValue(1, 4, itemLevel));
    }

    [Fact]
    public void ReturnIUEffectValue_Sort1_EligibleCategories()
    {
        Assert.Equal(18, StatCalculator.ReturnIUEffectValue(1, 4, 100));
        for (var category = 13; category <= 21; category++)
            Assert.Equal(18, StatCalculator.ReturnIUEffectValue(1, category, 100));
    }

    [Theory]
    [InlineData(2, 8, 2)]
    [InlineData(2, 9, 8)]
    [InlineData(2, 10, 2)]
    [InlineData(2, 12, 1)]
    [InlineData(3, 10, 17)]
    [InlineData(3, 13, 7)]
    [InlineData(3, 21, 7)]
    [InlineData(4, 9, 1)]
    [InlineData(4, 12, 2)]
    [InlineData(5, 11, 3)]
    [InlineData(6, 7, 1)]
    public void ReturnIUEffectValue_Sorts2To6_BaseKRows(int effectSort, int category, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnIUEffectValue(effectSort, category, 100));
    }

    [Theory]
    [InlineData(2, 7)]
    [InlineData(2, 11)]
    [InlineData(3, 9)]
    [InlineData(3, 12)]
    [InlineData(4, 8)]
    [InlineData(4, 10)]
    [InlineData(5, 10)]
    [InlineData(5, 12)]
    [InlineData(6, 8)]
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
        Assert.Equal(0, StatCalculator.ReturnIUEffectValue(effectSort, 10, 100));
    }

    [Fact]
    public void ReturnIUEffectValue_Level146AndAbove_IsZero_EverySort()
    {
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
        Assert.Equal(11, StatCalculator.ReturnIUEffectValue(1, 4, 0));
    }

    [Theory]
    [InlineData(5, 90)]
    [InlineData(0, 0)]
    [InlineData(-3, -54)]
    public void IUEffectSlotContribution_MultipliesPerPointByCount(int iuPointCount, int expected)
    {
        Assert.Equal(expected, StatCalculator.IUEffectSlotContribution(1, 4, 100, iuPointCount));
    }

    [Fact]
    public void IUEffectSlotContribution_IneligibleItem_IsZeroRegardlessOfCount()
    {
        Assert.Equal(0, StatCalculator.IUEffectSlotContribution(2, 7, 100, 99));
    }


    [Theory]
    [InlineData(DecorationStatKind.MaxLife, 41, 100)]
    [InlineData(DecorationStatKind.MaxLife, 60, 2000)]
    [InlineData(DecorationStatKind.MaxLife, 40, 0)]
    [InlineData(DecorationStatKind.MaxLife, 61, 0)]
    [InlineData(DecorationStatKind.MaxMana, 61, 125)]
    [InlineData(DecorationStatKind.MaxMana, 80, 2500)]
    [InlineData(DecorationStatKind.MaxMana, 60, 0)]
    [InlineData(DecorationStatKind.MaxMana, 81, 0)]
    [InlineData(DecorationStatKind.DefensePower, 1, 50)]
    [InlineData(DecorationStatKind.DefensePower, 20, 1000)]
    [InlineData(DecorationStatKind.DefensePower, 0, 0)]
    [InlineData(DecorationStatKind.DefensePower, 21, 0)]
    [InlineData(DecorationStatKind.AttackBlock, 21, 20)]
    [InlineData(DecorationStatKind.AttackBlock, 40, 400)]
    [InlineData(DecorationStatKind.AttackBlock, 20, 0)]
    [InlineData(DecorationStatKind.AttackBlock, 41, 0)]
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
        Assert.Equal(0, StatCalculator.ReturnNewValue(1, DecorationStatKind.AttackPower, octet));
    }


    [Theory]
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
    [InlineData(1, 200)]
    [InlineData(2, 400)]
    [InlineData(3, 600)]
    [InlineData(4, 800)]
    [InlineData(5, 1000)]
    [InlineData(25, 1000)]
    [InlineData(26, 0)]
    [InlineData(0, 0)]
    public void ReturnNewValue_HighOctet_DefensePower_ModuloFiveGated1To25(int octet, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnNewValue(2, DecorationStatKind.DefensePower, octet));
    }

    [Theory]
    [InlineData(26, 200)]
    [InlineData(27, 400)]
    [InlineData(30, 1000)]
    [InlineData(50, 1000)]
    [InlineData(25, 0)]
    [InlineData(51, 0)]
    public void ReturnNewValue_HighOctet_ElementDefense_ModuloFiveGated26To50(int octet, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReturnNewValue(2, DecorationStatKind.ElementDefensePower, octet));
    }

    [Theory]
    [InlineData(DecorationStatKind.MaxMana)]
    [InlineData(DecorationStatKind.AttackBlock)]
    [InlineData(DecorationStatKind.AttackPower)]
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
    [InlineData(5)]
    [InlineData(7)]
    public void ReturnNewValue_SelectorWithNoBranch_IsZero(int selector)
    {
        var stat = (DecorationStatKind)selector;
        for (var octet = 0; octet <= 100; octet++)
        {
            Assert.Equal(0, StatCalculator.ReturnNewValue(1, stat, octet));
            Assert.Equal(0, StatCalculator.ReturnNewValue(2, stat, octet));
        }
    }


    [Fact]
    public void ReturnNewStat_ZeroPackedValue_ShortCircuitsToZero()
    {
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.MaxLife, 0));
    }

    [Fact]
    public void ReturnNewStat_SumsThreeLowOctetsViaTSort1_AndHighOctetViaTSort2()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(45, 0, 0, 3);
        Assert.Equal(900, StatCalculator.ReturnNewStat(DecorationStatKind.MaxLife, packed));
    }

    [Fact]
    public void ReturnNewStat_AllThreeLowOctetsContributeIndependently()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(50, 55, 41, 48);
        Assert.Equal(1000 + 1500 + 100 + 2000, StatCalculator.ReturnNewStat(DecorationStatKind.MaxLife, packed));
    }

    [Fact]
    public void ReturnNewStat_LowOctetRoutesToTSort1_HighOctetRoutesToTSort2()
    {
        var lowOnly = StatCalculator.PackDecorationUpgradeOctets(3, 0, 0, 0);
        Assert.Equal(150, StatCalculator.ReturnNewStat(DecorationStatKind.DefensePower, lowOnly));

        var highOnly = StatCalculator.PackDecorationUpgradeOctets(0, 0, 0, 3);
        Assert.Equal(600, StatCalculator.ReturnNewStat(DecorationStatKind.DefensePower, highOnly));
    }

    [Fact]
    public void ReturnNewStat_SignedOctetWrap_StoredByteAbove127_ContributesZero()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(200, 0, 0, 0);
        Assert.NotEqual(0, packed);
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.DefensePower, packed));
    }

    [Fact]
    public void ReturnNewStat_MaxMana_IzOctetNeverContributes()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(0, 0, 0, 3);
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.MaxMana, packed));

        var withLowOctet = StatCalculator.PackDecorationUpgradeOctets(0, 61, 0, 3);
        Assert.Equal(125, StatCalculator.ReturnNewStat(DecorationStatKind.MaxMana, withLowOctet));
    }

    [Fact]
    public void ReturnNewStat_AttackPowerSelector_AlwaysZero()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(45, 55, 15, 3);
        Assert.Equal(0, StatCalculator.ReturnNewStat(DecorationStatKind.AttackPower, packed));
    }


    [Fact]
    public void PackDecorationUpgradeOctets_LaysOutOctetsLeastSignificantFirst()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(0x11, 0x22, 0x33, 0x44);
        Assert.Equal(0x44332211, packed);
    }

    [Fact]
    public void PackDecorationUpgradeOctets_NegativeOctet_WrapsToStoredByte()
    {
        var packed = StatCalculator.PackDecorationUpgradeOctets(-56, 0, 0, 0);
        Assert.Equal(200, packed & 0xFF);
    }


    [Theory]
    [InlineData(5, 11, true)]
    [InlineData(5, 12, true)]
    [InlineData(5, 13, true)]
    [InlineData(5, 14, true)]
    [InlineData(5, 10, false)]
    [InlineData(5, 15, false)]
    [InlineData(4, 11, false)]
    [InlineData(6, 12, false)]
    [InlineData(0, 0, false)]
    public void IsDecorationItem_Type5AndCategory11To14(int itemType, int equipInfoCategory, bool expected)
    {
        Assert.Equal(expected, StatCalculator.IsDecorationItem(itemType, equipInfoCategory));
    }
}
