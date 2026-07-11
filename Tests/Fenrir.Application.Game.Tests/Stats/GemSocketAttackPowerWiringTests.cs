using System.Buffers.Binary;
using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class GemSocketAttackPowerWiringTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    private static CharacterBaseAttributes Attributes(int strength = 0, int intelligence = 0, short level = 1)
    {
        return new CharacterBaseAttributes(0, strength, intelligence, 0, level, 0, 0, 0, 0, 0);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels(LevelRowDto row)
    {
        return new Dictionary<short, LevelRowDto> { [row.Level] = row }.ToFrozenDictionary();
    }

    private static LevelRowDto LevelRow(short level)
    {
        return new LevelRowDto(level, 0, 100, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static ItemRowDto Item(int itemId, byte sort = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
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

    private static FrozenDictionary<int, GemSocketRowDto> EffectTable(params GemSocketRowDto[] rows)
    {
        return rows.ToFrozenDictionary(static r => StatCalculator.GemSocketTypeValueKey((byte)r.Type, (byte)r.Value02));
    }

    [Fact]
    public void ComputeAttackPower_NoGemSocketTableSupplied_IgnoresPopulatedSocketBlob()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1));
        var (p1, p2, p3) = Pack(1, (2, 50));
        var equipment = new[] { new EquippedItemSlot(0, Item(1), 0, 0, 0, 0, p1, p2, p3) };

        var withoutTable = StatCalculator.ComputeBaseStats(attributes, equipment, levels);
        var withNoEquipmentAtAll = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(withNoEquipmentAtAll.AttackPower, withoutTable.AttackPower);
    }

    [Fact]
    public void ComputeAttackPower_FoldsGemSocketContribution_FromASingleEquippedSlot()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1));
        var (p1, p2, p3) = Pack(1, (2, 50));
        var equipment = new[] { new EquippedItemSlot(0, Item(1), 0, 0, 0, 0, p1, p2, p3) };
        var table = EffectTable(new GemSocketRowDto(1, 2, 0, 50, 77, 0));

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var withGem = StatCalculator.ComputeBaseStats(attributes, equipment, levels,
            gemSocketsByTypeAndValue: table);

        Assert.Equal(baseline.AttackPower + 77, withGem.AttackPower);
    }

    [Fact]
    public void ComputeAttackPower_SumsGemSocketContribution_AcrossMultipleOccupiedSlots()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1));
        var (slot0P1, slot0P2, slot0P3) = Pack(1, (2, 50));
        var (slot1P1, slot1P2, slot1P3) = Pack(1, (3, 10));
        var equipment = new[]
        {
            new EquippedItemSlot(0, Item(1), 0, 0, 0, 0, slot0P1, slot0P2, slot0P3),
            new EquippedItemSlot(2, Item(2), 0, 0, 0, 0, slot1P1, slot1P2, slot1P3)
        };
        var table = EffectTable(
            new GemSocketRowDto(1, 2, 0, 50, 77, 0),
            new GemSocketRowDto(2, 3, 0, 10, 15, 0));

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var withGems = StatCalculator.ComputeBaseStats(attributes, equipment, levels,
            gemSocketsByTypeAndValue: table);

        Assert.Equal(baseline.AttackPower + 77 + 15, withGems.AttackPower);
    }

    [Fact]
    public void ComputeAttackPower_UnmatchedGemSocketRow_ContributesZero_NoThrow()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1));
        var (p1, p2, p3) = Pack(1, (2, 99));
        var equipment = new[] { new EquippedItemSlot(0, Item(1), 0, 0, 0, 0, p1, p2, p3) };
        var table = EffectTable(new GemSocketRowDto(1, 2, 0, 50, 77, 0));

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var withGem = StatCalculator.ComputeBaseStats(attributes, equipment, levels,
            gemSocketsByTypeAndValue: table);

        Assert.Equal(baseline.AttackPower, withGem.AttackPower);
    }

    [Fact]
    public void ComputeEffectiveStats_ThreadsGemSocketTable_ThroughToComputeBaseStats()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1));
        var (p1, p2, p3) = Pack(1, (2, 50));
        var equipment = new[] { new EquippedItemSlot(0, Item(1), 0, 0, 0, 0, p1, p2, p3) };
        var table = EffectTable(new GemSocketRowDto(1, 2, 0, 50, 77, 0));

        var withoutTable = StatCalculator.ComputeEffectiveStats(attributes, equipment, levels);
        var withTable = StatCalculator.ComputeEffectiveStats(attributes, equipment, levels,
            gemSocketsByTypeAndValue: table);

        Assert.Equal(withoutTable.AttackPower + 77, withTable.AttackPower);
    }
}
