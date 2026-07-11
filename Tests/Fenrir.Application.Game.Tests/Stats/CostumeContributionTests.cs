using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Stats;

public class CostumeContributionTests
{
    [Theory]
    [InlineData(301)]
    [InlineData(402)]
    [InlineData(2146)]
    [InlineData(2148)]
    [InlineData(1801)]
    [InlineData(1893)]
    [InlineData(17701)]
    [InlineData(17703)]
    [InlineData(18124)]
    [InlineData(18132)]
    [InlineData(93301)]
    [InlineData(93316)]
    [InlineData(93317)]
    [InlineData(93330)]
    [InlineData(93334)]
    [InlineData(93381)]
    [InlineData(93385)]
    [InlineData(93405)]
    [InlineData(76524)]
    [InlineData(76526)]
    public void ValidCostume_IncludesEnumeratedIds(int costumeId)
    {
        Assert.True(StatCalculator.IsValidCostume(costumeId));
        Assert.Contains(costumeId, (IEnumerable<int>)StatCalculator.ValidCostumeIds);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(403)]
    [InlineData(93331)]
    [InlineData(93332)]
    [InlineData(93333)]
    [InlineData(93382)]
    [InlineData(93383)]
    [InlineData(93384)]
    [InlineData(76523)]
    [InlineData(76527)]
    [InlineData(0)]
    public void ValidCostume_ExcludesCommentedOutAndGapIds(int costumeId)
    {
        Assert.False(StatCalculator.IsValidCostume(costumeId));
        Assert.DoesNotContain(costumeId, (IEnumerable<int>)StatCalculator.ValidCostumeIds);
    }


    [Theory]
    [InlineData(101)]
    [InlineData(151)]
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
    [InlineData(100)]
    [InlineData(152)]
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
        foreach (var id in StatCalculator.ValidCostumeIds)
            Assert.False(StatCalculator.IsDecoStatCostume(id));

        int[] decoIds =
        [
            101, 151, 594, 595, 596, 1385, 1389, 1393, 1483, 1484, 1485, 2307, 2308, 2309, 8010, 8011, 8012, 91483,
            91488
        ];
        foreach (var id in decoIds)
            Assert.False(StatCalculator.IsValidCostume(id));
    }


    [Fact]
    public void CostumeBaseStatBlock_ItemNotFound_IsAllZero()
    {
        var block = StatCalculator.ComputeCostumeBaseStatBlock(301, false, 50, 60, 70, 80);
        Assert.Equal(default, block);
    }

    [Fact]
    public void CostumeBaseStatBlock_PlainItem_CopiesRawStats()
    {
        var block = StatCalculator.ComputeCostumeBaseStatBlock(500, true, 5, 6, 7, 8);
        Assert.Equal(new CostumeBaseStatBlock(5, 6, 7, 8), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_ValidCostume_AddsFlat100ToEachStat()
    {
        var block = StatCalculator.ComputeCostumeBaseStatBlock(301, true, 10, 20, 30, 40);
        Assert.Equal(new CostumeBaseStatBlock(110, 120, 130, 140), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_DecoItem_ClampsEachStatUpTo100()
    {
        var block = StatCalculator.ComputeCostumeBaseStatBlock(101, true, 10, 20, 30, 40);
        Assert.Equal(new CostumeBaseStatBlock(100, 100, 100, 100), block);
    }

    [Fact]
    public void CostumeBaseStatBlock_DecoItem_LeavesStatsAtOrAbove100Unchanged()
    {
        var block = StatCalculator.ComputeCostumeBaseStatBlock(151, true, 150, 20, 200, 99);
        Assert.Equal(new CostumeBaseStatBlock(150, 100, 200, 100), block);
    }


    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(96, 96)]
    [InlineData(127, 127)]
    [InlineData(128, -128)]
    [InlineData(200, -56)]
    [InlineData(255, -1)]
    [InlineData(256, 0)]
    [InlineData(0x0160, 96)]
    public void ReadSignedLowByte_InterpretsLowByteAsSignedChar(int packedWord, int expected)
    {
        Assert.Equal(expected, StatCalculator.ReadSignedLowByte(packedWord));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(20)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    public void DecodeCostumeEnchantCs_IndexOutsideGate_IsZero(int costumeIndex)
    {
        ReadOnlySpan<int> date = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        Assert.Equal(0, StatCalculator.DecodeCostumeEnchantCs(costumeIndex, date));
    }

    [Theory]
    [InlineData(10, 0, 11)]
    [InlineData(11, 1, 22)]
    [InlineData(15, 5, 66)]
    [InlineData(19, 9, 110)]
    public void DecodeCostumeEnchantCs_SelectsSlotByIndexModuloTen(int costumeIndex, int expectedSlot, int lowByte)
    {
        var raw = new int[10];
        raw[expectedSlot] = lowByte;
        ReadOnlySpan<int> date = raw;

        Assert.Equal(lowByte, StatCalculator.DecodeCostumeEnchantCs(costumeIndex, date));
    }

    [Fact]
    public void DecodeCostumeEnchantCs_NegativeLowByte_ReadsNegative()
    {
        var raw = new int[10];
        raw[3] = 200;
        ReadOnlySpan<int> date = raw;

        Assert.Equal(-56, StatCalculator.DecodeCostumeEnchantCs(13, date));
    }

    [Fact]
    public void DecodeCostumeEnchantCs_ShortSpan_IsZeroDefensively()
    {
        ReadOnlySpan<int> date = [11, 22];
        Assert.Equal(0, StatCalculator.DecodeCostumeEnchantCs(15, date));
    }


    [Fact]
    public void VitKiStrWisContributions_AddCsWithoutPositiveGuard()
    {
        var block = new CostumeBaseStatBlock(110, 120, 130, 140);

        Assert.Equal(115, StatCalculator.CostumeVitalityContribution(block, 5));
        Assert.Equal(135, StatCalculator.CostumeKiContribution(block, 5));
        Assert.Equal(125, StatCalculator.CostumeStrengthContribution(block, 5));
        Assert.Equal(145, StatCalculator.CostumeWisdomContribution(block, 5));

        Assert.Equal(54, StatCalculator.CostumeVitalityContribution(block, -56));
        Assert.Equal(74, StatCalculator.CostumeKiContribution(block, -56));
        Assert.Equal(64, StatCalculator.CostumeStrengthContribution(block, -56));
        Assert.Equal(84, StatCalculator.CostumeWisdomContribution(block, -56));
    }

    [Fact]
    public void KiContribution_ReadsIntelligence_WisdomContribution_ReadsDexterity()
    {
        var block = new CostumeBaseStatBlock(1, 2, 3, 4);

        Assert.Equal(3, StatCalculator.CostumeKiContribution(block, 0));
        Assert.Equal(4, StatCalculator.CostumeWisdomContribution(block, 0));
        Assert.Equal(1, StatCalculator.CostumeVitalityContribution(block, 0));
        Assert.Equal(2, StatCalculator.CostumeStrengthContribution(block, 0));
    }

    [Theory]
    [InlineData(96, 10)]
    [InlineData(95, 9)]
    [InlineData(97, 9)]
    [InlineData(100, 10)]
    [InlineData(90, 9)]
    [InlineData(9, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(-128, 0)]
    public void CriticalContribution_GuardsPositive_WithNinetySixSentinel(int cs, int expected)
    {
        Assert.Equal(expected, StatCalculator.CostumeCriticalContribution(cs));
    }


    [Fact]
    public void CriticalContribution_NinetySixSentinel_MatchesCostumeEnchantHardCap()
    {
        Assert.Equal(96, CostumeImproveResolver.MaxCostumeImprove);
    }

    [Fact]
    public void CriticalContribution_AtCap_IsDistinguishableFromTheNearMaxRange()
    {
        for (var cs = 90; cs <= 95; cs++)
            Assert.Equal(9, StatCalculator.CostumeCriticalContribution(cs));

        Assert.Equal(10, StatCalculator.CostumeCriticalContribution(96));
        Assert.NotEqual(95 / 10, StatCalculator.CostumeCriticalContribution(96));
    }

    [Fact]
    public void LuckContribution_AddsCsTimesTwo_PlusFlat100ForValidCostume()
    {
        Assert.Equal(120, StatCalculator.CostumeLuckContribution(301, 10));

        Assert.Equal(20, StatCalculator.CostumeLuckContribution(500, 10));

        Assert.Equal(100, StatCalculator.CostumeLuckContribution(301, 0));

        Assert.Equal(90, StatCalculator.CostumeLuckContribution(301, -5));

        Assert.Equal(-10, StatCalculator.CostumeLuckContribution(500, -5));
    }
}
