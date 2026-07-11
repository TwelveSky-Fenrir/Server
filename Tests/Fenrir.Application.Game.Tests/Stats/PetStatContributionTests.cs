using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class PetStatContributionTests
{
    private const int AmuletSort = 28;
    private const int GrowPetSort = 22;

    private const int Tier0Max = 40_000_000;


    private static readonly CharacterBaseAttributes NeutralAttributes = new(
        0, 0, 0, 0,
        1, 0, 0, 0, 0, 0);

    private static readonly FrozenDictionary<short, LevelRowDto> NeutralLevels =
        new Dictionary<short, LevelRowDto> { [1] = new(1, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0) }
            .ToFrozenDictionary();

    private static int Pack(int isByte, int iuByte = 0, int imByte = 0, int izByte = 0)
    {
        return (isByte & 0xFF) | ((iuByte & 0xFF) << 8) | ((imByte & 0xFF) << 16) | ((izByte & 0xFF) << 24);
    }


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
        var packed = Pack(0xFF, 0x80);
        Assert.Equal(-1, StatCalculator.DecodePetIsByte(packed));
        Assert.Equal(-128, StatCalculator.DecodePetIuByte(packed));
    }


    [Theory]
    [InlineData(1, 10, 30)]
    [InlineData(1, 15, 180)]
    [InlineData(1, 19, 300)]
    [InlineData(2, 20, 200)]
    [InlineData(2, 29, 2000)]
    [InlineData(3, 30, 20)]
    [InlineData(3, 39, 200)]
    [InlineData(4, 40, 250)]
    [InlineData(4, 49, 2500)]
    [InlineData(5, 50, 100)]
    [InlineData(5, 59, 1000)]
    [InlineData(6, 60, 100)]
    [InlineData(6, 69, 1000)]
    public void GradedIsBonus_MatchesLadder(int statType, int isByte, int expected)
    {
        var bonus = StatCalculator.PetGradedIsBonus(76500, AmuletSort, Pack(isByte), statType);
        Assert.Equal(expected, bonus);
    }

    [Fact]
    public void GradedIsBonus_ZeroWhenTensDigitDoesNotMatchRequestedType()
    {
        Assert.Equal(0, StatCalculator.PetGradedIsBonus(76500, AmuletSort, Pack(25), 1));
    }

    [Fact]
    public void GradedIsBonus_ZeroForNonAmuletSort()
    {
        Assert.Equal(0, StatCalculator.PetGradedIsBonus(76500, GrowPetSort, Pack(15), 1));
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
        Assert.Equal(0, StatCalculator.PetGradedIsBonus(excludedId, AmuletSort, Pack(15), 1));
    }


    [Fact]
    public void GradedIuBonus_ReturnsRawGradeDigitWhenTensMatches()
    {
        Assert.Equal(7, StatCalculator.PetGradedIuBonus(76500, AmuletSort, Pack(0, 27), 2));
    }

    [Fact]
    public void GradedIuBonus_ZeroWhenTensDoesNotMatch()
    {
        Assert.Equal(0, StatCalculator.PetGradedIuBonus(76500, AmuletSort, Pack(0, 27), 3));
    }

    [Fact]
    public void GradedIuBonus_ZeroForExcludedIndex()
    {
        Assert.Equal(0, StatCalculator.PetGradedIuBonus(2300, AmuletSort, Pack(0, 27), 2));
    }


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
    [InlineData(50, 0.3f)]
    [InlineData(51, 0.3f)]
    [InlineData(52, 0.9f)]
    [InlineData(53, 1.2f)]
    [InlineData(59, 3.0f)]
    public void GradedImBonus_Type5CriticalReplicatesGrade1Anomaly(int imByte, float expected)
    {
        var bonus = StatCalculator.PetGradedImBonus(76500, AmuletSort, Pack(0, 0, imByte), 5);
        Assert.Equal(expected, bonus);
    }

    [Fact]
    public void GradedImBonus_HasNoType6Ladder()
    {
        Assert.Equal(0f, StatCalculator.PetGradedImBonus(76500, AmuletSort, Pack(0, 0, 60), 6));
    }


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
        Assert.Equal(0, StatCalculator.PetAmuletAttackBonus(2200, AmuletSort));
        Assert.Equal(0, StatCalculator.PetAmuletDefenseBonus(2200, AmuletSort));
    }


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
        Assert.Equal(0f, StatCalculator.PetGrowPercent(Tier0Max, 0));
    }


    [Theory]
    [InlineData(0, 0)]
    [InlineData(1_000_000, 0)]
    [InlineData(41_000_000, 50)]
    [InlineData(50_000_000, 100)]
    [InlineData(60_000_000, 150)]
    [InlineData(70_000_000, 200)]
    [InlineData(80_000_000, 250)]
    [InlineData(120_000_000, 250)]
    public void SteppedAttackBonus_StepsOnGrowthPercent(int growValue, int expected)
    {
        Assert.Equal(expected, StatCalculator.PetSteppedAttackBonus(growValue, Tier0Max, 1));
    }

    [Fact]
    public void SteppedAttackBonus_ZeroWhenInactive()
    {
        Assert.Equal(0, StatCalculator.PetSteppedAttackBonus(80_000_000, Tier0Max, 0));
    }

    [Fact]
    public void SteppedAttackBonus_ZeroForUnrecognisedIndex()
    {
        Assert.Equal(0, StatCalculator.PetSteppedAttackBonus(80_000_000, 0, 1));
    }


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
