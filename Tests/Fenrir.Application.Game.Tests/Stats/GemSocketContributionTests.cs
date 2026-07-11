using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B13 -- gem-socket stat routing (<c>GetSocketType</c>, Server/ts25zone/S07_MyGame03.cpp:10150-10464)
///     plus the <c>SOCKET_GEM_V2</c> decode and per-item aggregation (<c>GetSocketInfo</c>, :10107-10148). These
///     tests pin only the ROUTING (which effect-table column each (stat,gem-type,gem-value) triple reads) and the
///     decode/aggregation shape -- the per-row magnitudes themselves are data-file-driven (world.GemSockets), so
///     every value assertion here uses a synthetic effect table, never a hardcoded legacy magnitude.
///     <para>
///         Reachability: only <see cref="GemSocketStatKind.AttackPower" /> is live in the shipped build.
///         WORKSTREAM B13-socket-prerequisites closed the two wiring gaps this remark used to describe --
///         <see cref="EquippedItemSlot" /> now carries the packed socket blob and
///         <c>WorldDataCache.GemSocketsByTypeAndValue</c> supplies the (Type,Value02)-keyed effect table -- so
///         <see cref="StatCalculator.ComputeAttackPower" /> now actually folds a socketed item's contribution;
///         see <c>Tests/.../Inventory/EquipmentServiceGemSocketWiringTests.cs</c> for the end-to-end coverage.
///         This file keeps pinning the routing, value read, decode and aggregation in isolation, unchanged.
///     </para>
/// </summary>
public class GemSocketContributionTests
{
    // ---- helpers ----

    private static GemSocketRowDto Row(int gemSocketId, int type, int value02, int value03, int value04)
    {
        // GemSocketRowDto(GemSocketId, Type, Value01, Value02, Value03, Value04). Value01 is loaded-but-unused.
        return new GemSocketRowDto(gemSocketId, type, 0, value02, value03, value04);
    }

    private static FrozenDictionary<int, GemSocketRowDto> Table(params GemSocketRowDto[] rows)
    {
        return rows.ToFrozenDictionary(
            static r => StatCalculator.GemSocketTypeValueKey((byte)r.Type, (byte)r.Value02));
    }

    // Builds SOCKET_GEM_V2 as 3 little-endian ints: [0] unused, [1] count, then up to 5 (type,value) byte pairs.
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

    // ---- routing: stat type 1, Attack (the only production-live one) ----

    [Theory]
    [InlineData(1, 1, GemSocketColumn.Primary)] // attribute triple {1,6,11}
    [InlineData(1, 6, GemSocketColumn.Primary)]
    [InlineData(1, 11, GemSocketColumn.Primary)]
    [InlineData(1, 2, GemSocketColumn.None)] // attribute value not in the Attack triple
    [InlineData(1, 7, GemSocketColumn.None)]
    [InlineData(2, 50, GemSocketColumn.Primary)] // single/mixed types 2-8
    [InlineData(8, 50, GemSocketColumn.Primary)]
    [InlineData(9, 50, GemSocketColumn.None)] // type 9 is a Defense gem, contributes nothing to Attack
    [InlineData(0, 0, GemSocketColumn.None)]
    public void ResolveColumn_Attack(byte gemType, byte gemValue, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.AttackPower, gemType, gemValue));
    }

    // ---- routing: stat type 2, Defense (dead in prod, transcribed for completeness) ----

    [Theory]
    [InlineData(1, 2, GemSocketColumn.Primary)]
    [InlineData(1, 12, GemSocketColumn.Primary)]
    [InlineData(1, 1, GemSocketColumn.None)]
    [InlineData(9, 3, GemSocketColumn.Primary)]
    [InlineData(14, 3, GemSocketColumn.Primary)]
    [InlineData(2, 3, GemSocketColumn.Secondary)] // type 2 routes to Defense's secondary column
    [InlineData(3, 3, GemSocketColumn.None)]
    [InlineData(15, 3, GemSocketColumn.None)]
    public void ResolveColumn_Defense(byte gemType, byte gemValue, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.Defense, gemType, gemValue));
    }

    // ---- routing: the HP/MP paired attribute gem {3,8,13} feeds MaxLife primary AND MaxMana secondary ----

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

    // ---- routing: MaxLife / MaxMana gem-type sets ----

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

    // ---- routing: the Hit/Dodge paired attribute gem {5,10,15} ----

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

    // ---- routing: the Element-Attack / Element-Defense paired attribute gem {4,9,14} ----

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
    [InlineData(29, GemSocketColumn.Primary)] // the single Element-Attack primary type
    [InlineData(7, GemSocketColumn.Secondary)]
    [InlineData(27, GemSocketColumn.Secondary)]
    [InlineData(8, GemSocketColumn.None)]
    public void ResolveColumn_ElementAttack_Types(byte gemType, GemSocketColumn expected)
    {
        Assert.Equal(expected, StatCalculator.ResolveGemSocketColumn(GemSocketStatKind.ElementAttack, gemType, 50));
    }

    // Element-Defense has NO primary branch -- every routed gem reads the secondary column.
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

    // ---- routing: codes with no legacy branch (9, 10) and the untranscribed 11-19 band -> always None ----

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(19)]
    public void ResolveColumn_NoLiveOrUntranscribedStatKind_IsNone(int statCode)
    {
        // These stat codes are outside the transcribed 1-8 enum band; the default switch arm returns None.
        Assert.Equal(
            GemSocketColumn.None,
            StatCalculator.ResolveGemSocketColumn((GemSocketStatKind)statCode, 2, 50));
    }

    // ---- value resolution: column pick reads Value03 (primary) / Value04 (secondary) ----

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
        // Attack gem-type 1 value 6 routes to primary; lookup key is (1,6).
        var table = Table(Row(1, 1, 6, 25, 0));
        Assert.Equal(25, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 1, 6, table));
    }

    [Fact]
    public void ResolveValue_NoneRoute_IsZeroEvenWithMatchingRow()
    {
        // Gem-type 9 contributes nothing to Attack; a present (9,50) row must not be read.
        var table = Table(Row(1, 9, 50, 9999, 9999));
        Assert.Equal(0, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 9, 50, table));
    }

    [Fact]
    public void ResolveValue_AbsentRow_IsZero_NoCrash()
    {
        var table = Table(Row(1, 2, 50, 100, 0));
        // Routed (primary) but no (2,99) row present -> hardening deviation returns 0 instead of the legacy crash.
        Assert.Equal(0, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 2, 99, table));
    }

    [Fact]
    public void ResolveValue_BelowOneValueGuard_IsZero()
    {
        // Gem-type 2 routes primary regardless of value, but the legacy below-1 value guard rejects value 0.
        var table = Table(Row(1, 2, 0, 500, 0));
        Assert.Equal(0, StatCalculator.ResolveGemSocketValue(GemSocketStatKind.AttackPower, 2, 0, table));
    }

    // ---- key builder ----

    [Fact]
    public void TypeValueKey_IsUnique_AcrossPlausibleRange()
    {
        var seen = new HashSet<int>();
        for (var type = 0; type <= 46; type++)
        for (var value = 0; value <= 100; value++)
            Assert.True(seen.Add(StatCalculator.GemSocketTypeValueKey((byte)type, (byte)value)));
    }

    // ---- SOCKET_GEM_V2 decode ----

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
        // Count byte says 7 but only 5 pairs fit; the decode caps at MaxSocketsPerItem rather than over-reading.
        var (p1, p2, p3) = Pack(7, (2, 1), (2, 2), (2, 3), (2, 4), (2, 5));
        Span<GemSocketSlot> slots = stackalloc GemSocketSlot[StatCalculator.MaxSocketsPerItem];
        Assert.Equal(5, StatCalculator.DecodeSocketGemV2(p1, p2, p3, slots));
    }

    // ---- per-item aggregation ----

    [Fact]
    public void Sum_FoldsActiveSockets_SkippingZeroType()
    {
        var table = Table(Row(1, 2, 50, 100, 0), Row(2, 1, 6, 25, 0));
        // count 3: (2,50)=100, (0,99)=skipped zero type, (1,6)=25 -> 125.
        var (p1, p2, p3) = Pack(3, (2, 50), (0, 99), (1, 6));

        Assert.Equal(125,
            StatCalculator.SumGemSocketContribution(GemSocketStatKind.AttackPower, p1, p2, p3, table));
    }

    [Fact]
    public void Sum_OnlyFirstCountSocketsAreRead()
    {
        var table = Table(Row(1, 1, 6, 25, 0), Row(2, 2, 50, 100, 0));
        // count is 1: only slot0 (1,6)=25 counts; slot1 (2,50)=100 is beyond the count and ignored.
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

    // ---- production-live gate ----

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
