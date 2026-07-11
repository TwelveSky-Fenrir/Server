using System.Buffers.Binary;
using System.Collections.Frozen;

namespace Fenrir.Application.Game.Stats;

public enum GemSocketColumn
{
    None = 0,
    Primary = 1,
    Secondary = 2
}

public enum GemSocketStatKind
{
    AttackPower = 1,
    Defense = 2,
    MaxLife = 3,
    MaxMana = 4,
    AttackSuccess = 5,
    AttackBlock = 6,
    ElementAttack = 7,
    ElementDefense = 8
}

public readonly record struct GemSocketSlot(byte GemType, byte GemValue);

public static partial class StatCalculator
{

        public const int MaxSocketsPerItem = 5;


        private static readonly FrozenSet<byte> AttributeAttackValues = new byte[] { 1, 6, 11 }.ToFrozenSet();

        private static readonly FrozenSet<byte> AttributeDefenseValues = new byte[] { 2, 7, 12 }.ToFrozenSet();

        private static readonly FrozenSet<byte> AttributeLifeManaValues = new byte[] { 3, 8, 13 }.ToFrozenSet();

        private static readonly FrozenSet<byte> AttributeHitDodgeValues = new byte[] { 5, 10, 15 }.ToFrozenSet();

        private static readonly FrozenSet<byte> AttributeElementValues = new byte[] { 4, 9, 14 }.ToFrozenSet();


    private static readonly FrozenSet<byte> AttackPrimaryTypes = new byte[] { 2, 3, 4, 5, 6, 7, 8 }.ToFrozenSet();

    private static readonly FrozenSet<byte> DefensePrimaryTypes = new byte[] { 9, 10, 11, 12, 13, 14 }.ToFrozenSet();

    private static readonly FrozenSet<byte> LifePrimaryTypes = new byte[] { 15, 16, 17, 18, 19 }.ToFrozenSet();
    private static readonly FrozenSet<byte> LifeSecondaryTypes = new byte[] { 3, 9 }.ToFrozenSet();

    private static readonly FrozenSet<byte> ManaPrimaryTypes = new byte[] { 20, 21, 22, 23 }.ToFrozenSet();
    private static readonly FrozenSet<byte> ManaSecondaryTypes = new byte[] { 4, 10, 15 }.ToFrozenSet();

    private static readonly FrozenSet<byte> HitPrimaryTypes = new byte[] { 24, 25, 26 }.ToFrozenSet();
    private static readonly FrozenSet<byte> HitSecondaryTypes = new byte[] { 5, 11, 16, 20 }.ToFrozenSet();

    private static readonly FrozenSet<byte> DodgePrimaryTypes = new byte[] { 27, 28 }.ToFrozenSet();
    private static readonly FrozenSet<byte> DodgeSecondaryTypes = new byte[] { 6, 12, 17, 21, 24 }.ToFrozenSet();

    private static readonly FrozenSet<byte> ElementAttackSecondaryTypes =
        new byte[] { 7, 13, 18, 22, 25, 27 }.ToFrozenSet();

    private static readonly FrozenSet<byte> ElementDefenseSecondaryTypes =
        new byte[] { 8, 14, 19, 23, 26, 28, 29 }.ToFrozenSet();

        public static bool IsGemSocketStatLiveInProduction(GemSocketStatKind statKind)
    {
        return statKind == GemSocketStatKind.AttackPower;
    }

        public static GemSocketColumn ResolveGemSocketColumn(GemSocketStatKind statKind, byte gemType, byte gemValue)
    {
        return statKind switch
        {
            GemSocketStatKind.AttackPower => ResolveAttack(gemType, gemValue),
            GemSocketStatKind.Defense => ResolveDefense(gemType, gemValue),
            GemSocketStatKind.MaxLife => ResolveMaxLife(gemType, gemValue),
            GemSocketStatKind.MaxMana => ResolveMaxMana(gemType, gemValue),
            GemSocketStatKind.AttackSuccess => ResolveAttackSuccess(gemType, gemValue),
            GemSocketStatKind.AttackBlock => ResolveAttackBlock(gemType, gemValue),
            GemSocketStatKind.ElementAttack => ResolveElementAttack(gemType, gemValue),
            GemSocketStatKind.ElementDefense => ResolveElementDefense(gemType, gemValue),
            _ => GemSocketColumn.None
        };
    }

    private static GemSocketColumn ResolveAttack(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeAttackValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        return AttackPrimaryTypes.Contains(gemType) ? GemSocketColumn.Primary : GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveDefense(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeDefenseValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (DefensePrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (gemType == 2) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveMaxLife(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeLifeManaValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (LifePrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (LifeSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveMaxMana(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeLifeManaValues.Contains(gemValue) ? GemSocketColumn.Secondary : GemSocketColumn.None;
        if (ManaPrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (ManaSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveAttackSuccess(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeHitDodgeValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (HitPrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (HitSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveAttackBlock(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeHitDodgeValues.Contains(gemValue) ? GemSocketColumn.Secondary : GemSocketColumn.None;
        if (DodgePrimaryTypes.Contains(gemType)) return GemSocketColumn.Primary;
        if (DodgeSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveElementAttack(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeElementValues.Contains(gemValue) ? GemSocketColumn.Primary : GemSocketColumn.None;
        if (gemType == 29) return GemSocketColumn.Primary;
        if (ElementAttackSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

    private static GemSocketColumn ResolveElementDefense(byte gemType, byte gemValue)
    {
        if (gemType == 1)
            return AttributeElementValues.Contains(gemValue) ? GemSocketColumn.Secondary : GemSocketColumn.None;
        if (ElementDefenseSecondaryTypes.Contains(gemType)) return GemSocketColumn.Secondary;
        return GemSocketColumn.None;
    }

        public static int GemSocketTypeValueKey(byte gemType, byte gemValue)
    {
        return (gemType << 8) | gemValue;
    }

        public static int ResolveGemSocketValue(GemSocketStatKind statKind, byte gemType, byte gemValue,
        FrozenDictionary<int, GemSocketRowDto> effectTable)
    {
        var column = ResolveGemSocketColumn(statKind, gemType, gemValue);
        if (column == GemSocketColumn.None) return 0;

        if (gemType < 1 || gemValue < 1) return 0;

        if (!effectTable.TryGetValue(GemSocketTypeValueKey(gemType, gemValue), out var row))
            return 0;

        return column == GemSocketColumn.Primary ? row.Value03 : row.Value04;
    }

        public static int DecodeSocketGemV2(int packed1, int packed2, int packed3, Span<GemSocketSlot> destination)
    {
        Span<byte> bytes = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes[..4], packed1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(4, 4), packed2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(8, 4), packed3);

        var count = Math.Min((int)bytes[1], MaxSocketsPerItem);
        for (var i = 0; i < count; i++)
            destination[i] = new GemSocketSlot(bytes[2 + i * 2], bytes[3 + i * 2]);

        return count;
    }

        public static int SumGemSocketContribution(GemSocketStatKind statKind, int packed1, int packed2, int packed3,
        FrozenDictionary<int, GemSocketRowDto> effectTable)
    {
        Span<GemSocketSlot> slots = stackalloc GemSocketSlot[MaxSocketsPerItem];
        var count = DecodeSocketGemV2(packed1, packed2, packed3, slots);

        var total = 0;
        for (var i = 0; i < count; i++)
        {
            var slot = slots[i];
            if (slot.GemType == 0) continue;
            total += ResolveGemSocketValue(statKind, slot.GemType, slot.GemValue, effectTable);
        }

        return total;
    }
}
