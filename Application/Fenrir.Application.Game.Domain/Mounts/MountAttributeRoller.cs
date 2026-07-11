using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     The three rolled-attribute mutations on a mount's packed power value (<see cref="MountPowerCodec" />):
///     Convert (op87 Sort 6), Delete (Sort 7), Transfer (Sort 8). Pure -- every random choice is drawn through
///     the injected <see cref="IRandomSource" /> in legacy call order, so tests pin exact outcomes. The caller
///     (<c>MountStateService</c> via <see cref="MountStateResolver" />) owns the surrounding gates
///     (experience-must-equal-100000, sum-below-25, material-item presence, disconnect vs. no-reply); this type
///     only performs the digit arithmetic once those gates have passed.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:6879-6931 (<c>ConvertAnimalStat</c> -- picks one of the eight
///     digits at random, returns its place-value 1/10/.../10,000,000; if that digit is already maxed at 9 it
///     walks forward to the next non-maxed digit) ; :6934-6945 (<c>DeleteAnimalStat</c> -- decrement the chosen
///     digit by 1, floored at 0) ; :6947-7017 (<c>TransferAnimalStat</c> -- an empty source digit returns 0
///     (failure); otherwise decrement the chosen digit and add 1 to a randomly chosen OTHER non-maxed digit).
///     <para>
///         Two prose-level interpretation choices the contract's cited ranges do not byte-pin, documented here
///         rather than guessed silently:
///     </para>
///     <para>
///         (1) Convert's random pick uses a 0-based index over the eight digits and its place-value set is the
///         ascending 1..10,000,000 the contract names, so index 0 maps to place 0 (ones) and index 7 to place 7
///         (ten-millions). The maxed-digit "walk forward" advances the place index and wraps modulo eight, which
///         is what makes the legacy's own "returned place-value is never zero" guard provably unreachable given
///         Convert's upstream sum-below-25 gate (at most 24 of the 72 possible points invested, so a non-maxed
///         digit always exists to land on). No carry can occur: the walk only ever lands on a digit below 9, so
///         adding its single place-value increments exactly that digit by one.
///     </para>
///     <para>
///         (2) Transfer's "randomly chosen other non-maxed digit" is drawn as a single uniform pick over the
///         filtered candidate list (every digit except the source that is below 9), one draw, so the Scripted
///         RandomSource draw order in tests is deterministic. If no other digit is non-maxed the source is still
///         decremented but nothing is added -- a net -1 -- since the contract specifies only the empty-source
///         failure, not a no-valid-target failure. Both specifics are flagged for a <c>cpp-zone-gameplay-analyst</c>
///         re-check against the exact C++ draw shape before treating them as byte-exact parity.
///     </para>
/// </remarks>
public static class MountAttributeRoller
{
    /// <summary>
    ///     <c>ConvertAnimalStat</c>: draws a random digit, walks forward past any maxed digit, and adds one to
    ///     the first non-maxed digit found. See this type's own remarks for the walk/wrap interpretation.
    /// </summary>
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

        // Every digit maxed -- unreachable behind Convert's own sum-below-25 gate; caller disconnects.
        return new ConvertRoll(false, 0, power);
    }

    /// <summary>
    ///     <c>DeleteAnimalStat</c>: decrements the wire-<paramref name="attributeIndex" /> (1-8) digit by one,
    ///     floored at 0. A digit already at 0 stays 0 -- there is no "nothing to delete" guard (the caller still
    ///     consumes the material item), matching the cited legacy behavior.
    /// </summary>
    public static int Delete(int power, int attributeIndex)
    {
        var placeIndex = MountPowerCodec.AttributeIndexToPlace(attributeIndex);
        var digit = MountPowerCodec.DigitAtPlace(power, placeIndex);
        return MountPowerCodec.WithDigitAtPlace(power, placeIndex, Math.Max(0, digit - 1));
    }

    /// <summary>
    ///     <c>TransferAnimalStat</c>: moves one point off the wire-<paramref name="attributeIndex" /> (1-8) digit
    ///     onto a randomly chosen other non-maxed digit. An empty source digit returns
    ///     <see cref="TransferRoll.Applied" /> false with the power unchanged (the caller reverts and disconnects).
    /// </summary>
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
            // No other digit can receive the point -- source already decremented, nothing to add. See remarks.
            return new TransferRoll(true, decremented);

        var targetPlace = candidates[random.NextInt32(candidateCount)];
        var targetDigit = MountPowerCodec.DigitAtPlace(decremented, targetPlace);
        return new TransferRoll(true, MountPowerCodec.WithDigitAtPlace(decremented, targetPlace, targetDigit + 1));
    }

    /// <param name="Applied">
    ///     False only in the (gate-unreachable) case where every digit is already maxed and no place-value could
    ///     be added -- the caller treats this the same as the legacy's zero-return disconnect.
    /// </param>
    /// <param name="PlaceValueAdded">
    ///     The place-value the roll added to the power (0 when <paramref name="Applied" /> is
    ///     false).
    /// </param>
    /// <param name="NewPower">The resulting packed power value.</param>
    public readonly record struct ConvertRoll(bool Applied, int PlaceValueAdded, int NewPower);

    /// <param name="Applied">
    ///     False when the source digit was already 0 (the empty-source failure -- caller reverts and
    ///     disconnects).
    /// </param>
    /// <param name="NewPower">The resulting packed power value (unchanged when <paramref name="Applied" /> is false).</param>
    public readonly record struct TransferRoll(bool Applied, int NewPower);
}
