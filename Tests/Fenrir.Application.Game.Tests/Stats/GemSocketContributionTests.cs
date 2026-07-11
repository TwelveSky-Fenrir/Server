using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class GemSocketContributionTests
{

    private static GemSocketRowDto Row(int gemSocketId, int type, int value02, int value03, int value04)
    {
        return new GemSocketRowDto(gemSocketId, type, 0, value02, value03, value04);
    }

    private static FrozenDictionary<int, GemSocketRowDto> Table(params GemSocketRowDto[] rows)
    {
        return rows.ToFrozenDictionary(
            static r => StatCalculator.GemSocketTypeValueKey((byte)r.Type, (byte)r.Value02));
    }

    private static (int P1, int P2, int P3) Pack(byte count, params (byte Type, byte Value)[] pairs)
    {
        Span<byte> bytes = stackalloc byte[12];
        bytes[1] = count;
        for (var i = 0; i < pairs.Length && i < 5; i++)
        {
            bytes[2 + i * 2] = pairs[i].Type;
            bytes[3 + i * 2] = pairs[i].Value;
        }

        return (
            BinaryPrimitives.ReadInt32LittleEndian(bytes[..4]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4)),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4)));
    }


    [Theory]
    [InlineData(1, 1, GemSocketColumn.Primary)]
    [InlineData(1, 6, GemSocketColumn.Primary)]
    [InlineData(1, 11, GemSocketColumn.Primary)]
    [InlineData(1, 2, GemSocketColumn.None)]
    [InlineData(1, 7, GemSocketColumn.None)]
    [InlineData(2, 50, GemSocketColumn.Primary)]
    [InlineData(8, 50, GemSocketColumn.Primary)]
    [InlineData(9, 50, GemSocketColumn.None)]
    [InlineData(0, 0, GemSocketColumn.None)]
    public void ResolveColumn_Attack(byte gemType, byte gemValue, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.AttackPower, gemType, gemValue));
    }


    [Theory]
    [InlineData(1, 2, GemSocketColumn.Primary)]
    [InlineData(1, 12, GemSocketColumn.Primary)]
    [InlineData(1, 1, GemSocketColumn.None)]
    [InlineData(9, 3, GemSocketColumn.Primary)]
    [InlineData(14, 3, GemSocketColumn.Primary)]
    [InlineData(2, 3, GemSocketColumn.Secondary)]
    [InlineData(3, 3, GemSocketColumn.None)]
    [InlineData(15, 3, GemSocketColumn.None)]
    public void ResolveColumn_Defense(byte gemType, byte gemValue, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.Defense, gemType, gemValue));
    }


    [Theory]
    [InlineData(3, GemSocketStatKind.MaxLife, GemSocketColumn.Primary)]
    [InlineData(8, GemSocketStatKind.MaxLife, GemSocketColumn.Primary)]
    [InlineData(13, GemSocketStatKind.MaxLife, GemSocketColumn.Primary)]
    [InlineData(3, GemSocketStatKind.MaxMana, GemSocketColumn.Secondary)]
    [InlineData(8, GemSocketStatKind.MaxMana, GemSocketColumn.Secondary)]
    [InlineData(13, GemSocketStatKind.MaxMana, GemSocketColumn.Secondary)]
    public void ResolveColumn_AttributeGem_HpMpPair(byte gemValue, GemSocketStatKind stat, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(stat, 1, gemValue));
    }


    [Theory]
    [InlineData(15, GemSocketColumn.Primary)]
    [InlineData(19, GemSocketColumn.Primary)]
    [InlineData(3, GemSocketColumn.Secondary)]
    [InlineData(9, GemSocketColumn.Secondary)]
    [InlineData(20, GemSocketColumn.None)]
    public void ResolveColumn_MaxLife_Types(byte gemType, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.MaxLife, gemType, 50));
    }

    [Theory]
    [InlineData(20, GemSocketColumn.Primary)]
    [InlineData(23, GemSocketColumn.Primary)]
    [InlineData(4, GemSocketColumn.Secondary)]
    [InlineData(10, GemSocketColumn.Secondary)]
    [InlineData(15, GemSocketColumn.Secondary)]
    [InlineData(3, GemSocketColumn.None)]
    public void ResolveColumn_MaxMana_Types(byte gemType, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.MaxMana, gemType, 50));
    }


    [Theory]
    [InlineData(5, GemSocketStatKind.AttackSuccess, GemSocketColumn.Primary)]
    [InlineData(15, GemSocketStatKind.AttackSuccess, GemSocketColumn.Primary)]
    [InlineData(5, GemSocketStatKind.AttackBlock, GemSocketColumn.Secondary)]
    [InlineData(10, GemSocketStatKind.AttackBlock, GemSocketColumn.Secondary)]
    public void ResolveColumn_AttributeGem_HitDodgePair(byte gemValue, GemSocketStatKind stat, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(stat, 1, gemValue));
    }

    [Theory]
    [InlineData(GemSocketStatKind.AttackSuccess, 24, GemSocketColumn.Primary)]
    [InlineData(GemSocketStatKind.AttackSuccess, 26, GemSocketColumn.Primary)]
    [InlineData(GemSocketStatKind.AttackSuccess, 5, GemSocketColumn.Secondary)]
    [InlineData(GemSocketStatKind.AttackSuccess, 20, GemSocketColumn.Secondary)]
    [InlineData(GemSocketStatKind.AttackBlock, 27, GemSocketColumn.Primary)]
    [InlineData(GemSocketStatKind.AttackBlock, 28, GemSocketColumn.Primary)]
    [InlineData(GemSocketStatKind.AttackBlock, 6, GemSocketColumn.Secondary)]
    [InlineData(GemSocketStatKind.AttackBlock, 24, GemSocketColumn.Secondary)]
    public void ResolveColumn_HitDodge_Types(GemSocketStatKind stat, byte gemType, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(stat, gemType, 50));
    }


    [Theory]
    [InlineData(4, GemSocketStatKind.ElementAttack, GemSocketColumn.Primary)]
    [InlineData(14, GemSocketStatKind.ElementAttack, GemSocketColumn.Primary)]
    [InlineData(4, GemSocketStatKind.ElementDefense, GemSocketColumn.Secondary)]
    [InlineData(9, GemSocketStatKind.ElementDefense, GemSocketColumn.Secondary)]
    public void ResolveColumn_AttributeGem_ElementPair(byte gemValue, GemSocketStatKind stat, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(stat, 1, gemValue));
    }

    [Theory]
    [InlineData(29, GemSocketColumn.Primary)]
    [InlineData(7, GemSocketColumn.Secondary)]
    [InlineData(27, GemSocketColumn.Secondary)]
    [InlineData(8, GemSocketColumn.None)]
    public void ResolveColumn_ElementAttack_Types(byte gemType, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.ElementAttack, gemType, 50));
    }

    [Theory]
    [InlineData(8, GemSocketColumn.Secondary)]
    [InlineData(14, GemSocketColumn.Secondary)]
    [InlineData(29, GemSocketColumn.Secondary)]
    [InlineData(7, GemSocketColumn.None)]
    public void ResolveColumn_ElementDefense_Types(byte gemType, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.ElementDefense, gemType, 50));
    }

    [Fact]
    public void ResolveColumn_ElementDefense_NeverPrimary()
    {
        for (var gemType = 0; gemType <= 46; gemType++)
        for (var gemValue = 0; gemValue <= 33; gemValue++)
            Assert.NotEqual(
                GemSocketColumn.Primary,
                StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.ElementDefense, (byte)gemType, (byte)gemValue));
    }


    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(19)]
    public void ResolveColumn_NoLiveOrUntranscribedStatKind_IsNone(int statCode)
    {
        Assert.Equal(
            GemSocketColumn.None,
            StatCalculator.ResolveGemSocketColumn((GemSocketStatKind)statCode, 2, 50));
    }


    [Fact]
    public void ResolveValue_Primary_ReadsValue03()
    {
        var table = Table(Row(1, 2, 50, 100, 7));
        Assert.Equal(100, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 2, 50, table));
    }

    [Fact]
    public void ResolveValue_Secondary_ReadsValue04()
    {
        var table = Table(Row(1, 8, 40, 11, 222));
        Assert.Equal(222, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.ElementDefense, 8, 40, table));
    }

    [Fact]
    public void ResolveValue_AttributeGem_EndToEnd()
    {
        var table = Table(Row(1, 1, 6, 25, 0));
        Assert.Equal(25, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 1, 6, table));
    }

    [Fact]
    public void ResolveValue_NoneRoute_IsZeroEvenWithMatchingRow()
    {
        var table = Table(Row(1, 9, 50, 9999, 9999));
        Assert.Equal(0, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 9, 50, table));
    }

    [Fact]
    public void ResolveValue_AbsentRow_IsZero_NoCrash()
    {
        var table = Table(Row(1, 2, 50, 100, 0));
        Assert.Equal(0, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 2, 99, table));
    }

    [Fact]
    public void ResolveValue_BelowOneValueGuard_IsZero()
    {
        var table = Table(Row(1, 2, 0, 500, 0));
        Assert.Equal(0, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 2, 0, table));
    }


    [Fact]
    public void TypeValueKey_IsUnique_AcrossPlausibleRange()
    {
        var seen = new HashSet<int>();
        for (var type = 0; type <= 46; type++)
        for (var value = 0; value <= 100; value++)
            Assert.True(seen.Add(StatCalculator.GemSocketTypeValueKey((byte)type, (byte)value)));
    }


    [Fact]
    public void Decode_UnpacksCountAndPairs()
    {
        var (p1, p2, p3) = Pack(2, (2, 50), (1, 6));
        Span<GemSocketSlot> slots = stackalloc GemSocketSlot[StatCalculator.MaxSocketsPerItem];
        var count = StatCalculator.DecodeSocketGemV2(p1, p2, p3, slots);

        Assert.Equal(2, count);
        Assert.Equal(new GemSocketSlot(2, 50), slots[0]);
        Assert.Equal(new GemSocketSlot(1, 6), slots[1]);
    }

    [Fact]
    public void Decode_ZeroCount_IsZero()
    {
        var (p1, p2, p3) = Pack(0);
        Span<GemSocketSlot> slots = stackalloc GemSocketSlot[StatCalculator.MaxSocketsPerItem];
        Assert.Equal(0, StatCalculator.DecodeSocketGemV2(p1, p2, p3, slots));
    }

    [Fact]
    public void Decode_CountAboveFive_IsClampedToFive()
    {
        var (p1, p2, p3) = Pack(7, (2, 1), (2, 2), (2, 3), (2, 4), (2, 5));
        Span<GemSocketSlot> slots = stackalloc GemSocketSlot[StatCalculator.MaxSocketsPerItem];
        Assert.Equal(5, StatCalculator.DecodeSocketGemV2(p1, p2, p3, slots));
    }


    [Fact]
    public void Sum_FoldsActiveSockets_SkippingZeroType()
    {
        var table = Table(Row(1, 2, 50, 100, 0), Row(2, 1, 6, 25, 0));
        var (p1, p2, p3) = Pack(3, (2, 50), (0, 99), (1, 6));

        Assert.Equal(125,
            StatCalculator.SumGemSocketContribution(GemSocketStatKind.AttackPower, p1, p2, p3, table));
    }

    [Fact]
    public void Sum_OnlyFirstCountSocketsAreRead()
    {
        var table = Table(Row(1, 1, 6, 25, 0), Row(2, 2, 50, 100, 0));
        var (p1, p2, p3) = Pack(1, (1, 6), (2, 50));

        Assert.Equal(25,
            StatCalculator.SumGemSocketContribution(GemSocketStatKind.AttackPower, p1, p2, p3, table));
    }

    [Fact]
    public void Sum_ZeroCount_IsZero()
    {
        var table = Table(Row(1, 2, 50, 100, 0));
        var (p1, p2, p3) = Pack(0);

        Assert.Equal(0,
            StatCalculator.SumGemSocketContribution(GemSocketStatKind.AttackPower, p1, p2, p3, table));
    }


    [Fact]
    public void IsLiveInProduction_OnlyAttackPower()
    {
        Assert.True(StatCalculator.IsGemSocketStatLiveInProduction(GemSocketStatKind.AttackPower));
        foreach (var stat in new[]
                 {
                     GemSocketStatKind.Defense, GemSocketStatKind.MaxLife, GemSocketStatKind.MaxMana,
                     GemSocketStatKind.AttackSuccess, GemSocketStatKind.AttackBlock,
                     GemSocketStatKind.ElementAttack, GemSocketStatKind.ElementDefense
                 })
            Assert.False(StatCalculator.IsGemSocketStatLiveInProduction(stat));
    }
}
