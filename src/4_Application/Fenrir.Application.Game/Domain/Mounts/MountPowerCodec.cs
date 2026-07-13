using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountPowerCodec
{
    public const int DigitCount = 8;

    public const int MaxDigit = 9;

    private static readonly int[] PlaceValue =
        [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000];

    public static int PlaceValueAt(int placeIndex)
    {
        return PlaceValue[placeIndex];
    }

    public static int AttributeIndexToPlace(int attributeIndex)
    {
        return DigitCount - attributeIndex;
    }

    public static int DigitAtPlace(int power, int placeIndex)
    {
        return power / PlaceValue[placeIndex] % 10;
    }

    public static int DigitByAttributeIndex(int power, int attributeIndex)
    {
        return DigitAtPlace(power, AttributeIndexToPlace(attributeIndex));
    }

    public static int WithDigitAtPlace(int power, int placeIndex, int newDigit)
    {
        var current = DigitAtPlace(power, placeIndex);
        return power + (newDigit - current) * PlaceValue[placeIndex];
    }

    public static int DigitSum(int power)
    {
        var sum = 0;
        for (var placeIndex = 0; placeIndex < DigitCount; placeIndex++)
            sum += DigitAtPlace(power, placeIndex);
        return sum;
    }

    /// <summary>
    ///     Reassembles the raw encoded rolled-attribute power for one garage slot from its eight decoded
    ///     stat-slot digits held in <paramref name="rolledAttributes" /> (indexed
    ///     <c>slot * DigitCount + statSlotIndex</c>). Stat-slot index 0 (Attack) maps to the most-significant
    ///     place (10^7), index 7 (Element-Defense) to the units place (10^0), matching the
    ///     <c>StatCalculator.DecodeMountPowerDigits</c> digit layout.
    /// </summary>
    public static int EncodeSlot(ImmutableArray<int> rolledAttributes, int slot)
    {
        var baseIndex = slot * DigitCount;
        var power = 0;
        for (var statSlotIndex = 0; statSlotIndex < DigitCount; statSlotIndex++)
            power += rolledAttributes[baseIndex + statSlotIndex] * PlaceValue[DigitCount - 1 - statSlotIndex];
        return power;
    }
}
