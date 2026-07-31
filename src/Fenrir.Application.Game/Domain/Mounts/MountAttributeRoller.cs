using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountAttributeRoller
{
    public static ConvertRoll Convert(int power, IRandomSource random)
    {
        var pick = random.NextInt32(MountPowerCodec.DigitCount);

        for (var step = 0; step < MountPowerCodec.DigitCount; step++)
        {
            var placeIndex = ScanPlace(pick, step);
            if (MountPowerCodec.DigitAtPlace(power, placeIndex) >= MountPowerCodec.MaxDigit)
                continue;

            var placeValue = MountPowerCodec.PlaceValueAt(placeIndex);
            return new ConvertRoll(true, placeValue, power + placeValue);
        }

        return new ConvertRoll(false, 0, power);
    }

    public static int Delete(int power, int attributeIndex)
    {
        var placeIndex = MountPowerCodec.AttributeIndexToPlace(attributeIndex);
        var digit = MountPowerCodec.DigitAtPlace(power, placeIndex);
        return MountPowerCodec.WithDigitAtPlace(power, placeIndex, Math.Max(0, digit - 1));
    }

    public static TransferRoll Transfer(int power, int attributeIndex, IRandomSource random)
    {
        var sourcePlace = MountPowerCodec.AttributeIndexToPlace(attributeIndex);
        var sourceDigit = MountPowerCodec.DigitAtPlace(power, sourcePlace);

        if (sourceDigit == 0)
            return new TransferRoll(false, power);

        var decremented = MountPowerCodec.WithDigitAtPlace(power, sourcePlace, sourceDigit - 1);

        var pick = random.NextInt32(MountPowerCodec.DigitCount);

        for (var step = 0; step < MountPowerCodec.DigitCount; step++)
        {
            var placeIndex = ScanPlace(pick, step);
            if (placeIndex == sourcePlace)
                continue;

            var targetDigit = MountPowerCodec.DigitAtPlace(decremented, placeIndex);
            if (targetDigit >= MountPowerCodec.MaxDigit)
                continue;

            return new TransferRoll(true,
                MountPowerCodec.WithDigitAtPlace(decremented, placeIndex, targetDigit + 1));
        }

        return new TransferRoll(true, decremented);
    }

    private static int ScanPlace(int pick, int step)
    {
        return (pick + MountPowerCodec.DigitCount - step) % MountPowerCodec.DigitCount;
    }

    public readonly record struct ConvertRoll(bool Applied, int PlaceValueAdded, int NewPower);

    public readonly record struct TransferRoll(bool Applied, int NewPower);
}
