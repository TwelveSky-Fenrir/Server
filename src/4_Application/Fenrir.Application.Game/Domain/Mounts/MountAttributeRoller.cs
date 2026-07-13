using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountAttributeRoller
{
    public static ConvertRoll Convert(int power, IRandomSource random)
    {
        var pick = random.NextInt32(MountPowerCodec.DigitCount);

        for (var step = 0; step < MountPowerCodec.DigitCount; step++)
        {
            var placeIndex = (pick + step) % MountPowerCodec.DigitCount;
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

        Span<int> candidates = stackalloc int[MountPowerCodec.DigitCount];
        var candidateCount = 0;
        for (var placeIndex = 0; placeIndex < MountPowerCodec.DigitCount; placeIndex++)
        {
            if (placeIndex == sourcePlace)
                continue;
            if (MountPowerCodec.DigitAtPlace(decremented, placeIndex) >= MountPowerCodec.MaxDigit)
                continue;
            candidates[candidateCount++] = placeIndex;
        }

        if (candidateCount == 0)
            return new TransferRoll(true, decremented);

        var targetPlace = candidates[random.NextInt32(candidateCount)];
        var targetDigit = MountPowerCodec.DigitAtPlace(decremented, targetPlace);
        return new TransferRoll(true, MountPowerCodec.WithDigitAtPlace(decremented, targetPlace, targetDigit + 1));
    }

    public readonly record struct ConvertRoll(bool Applied, int PlaceValueAdded, int NewPower);

    public readonly record struct TransferRoll(bool Applied, int NewPower);
}
