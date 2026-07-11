namespace Fenrir.Application.Game.Domain.Mounts;

/// <summary>
///     The <c>aAnimalPower</c> packed integer: eight decimal digits, each digit one rolled mount attribute
///     holding a value 0-9. This is the exact shape persisted in <c>game.Characters.MountPower</c> (slot 0).
///     The runtime keeps the eight digits decoded on <see cref="World.PlayerRuntimeState.MountRolledAttributes" />
///     and their sum on <see cref="World.PlayerRuntimeState.MountRolledAttributeTotal" />, so this codec is the
///     boundary translation used to decode on world-entry hydration and re-encode on the rare persistence write,
///     plus the digit primitives the roll mechanics (<see cref="MountAttributeRoller" />) operate through.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame03.cpp:7020-7037 (<c>ParseAnimalStat</c> / <c>ReturnAnimalStat</c>
///     -- the eight-digit split and reassembly, and the attribute-index-to-digit mapping: attribute-index 1 is
///     the highest-place digit and 8 is the ones digit) ; Server/Header/function.h:2435-2447
///     (<c>ReturnAnimalStat</c> integer overload -- the digit sum, i.e. total invested points).
///     <para>
///         "Place index" here is the base-10 exponent: place 0 is the ones digit, place 7 is the ten-millions
///         digit. Wire "attribute index" is the legacy 1-8 addressing (1 = highest place = place 7, 8 = ones =
///         place 0), converted via <see cref="AttributeIndexToPlace" />.
///     </para>
/// </remarks>
public static class MountPowerCodec
{
    /// <summary>The number of decimal-digit attribute slots packed into a single power value.</summary>
    public const int DigitCount = 8;

    /// <summary>The highest value a single attribute digit can hold.</summary>
    public const int MaxDigit = 9;

    private static readonly int[] PlaceValue =
        [1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000];

    /// <summary>The positional weight of the digit at <paramref name="placeIndex" /> (0-7).</summary>
    public static int PlaceValueAt(int placeIndex)
    {
        return PlaceValue[placeIndex];
    }

    /// <summary>Wire attribute index (1-8, highest place first) to base-10 place index (0-7, ones first).</summary>
    public static int AttributeIndexToPlace(int attributeIndex)
    {
        return DigitCount - attributeIndex;
    }

    /// <summary>The 0-9 digit occupying <paramref name="placeIndex" /> (0-7) of <paramref name="power" />.</summary>
    public static int DigitAtPlace(int power, int placeIndex)
    {
        return power / PlaceValue[placeIndex] % 10;
    }

    /// <summary>The 0-9 digit addressed by wire <paramref name="attributeIndex" /> (1-8).</summary>
    public static int DigitByAttributeIndex(int power, int attributeIndex)
    {
        return DigitAtPlace(power, AttributeIndexToPlace(attributeIndex));
    }

    /// <summary>
    ///     <paramref name="power" /> with the digit at <paramref name="placeIndex" /> replaced by
    ///     <paramref name="newDigit" /> (expected 0-<see cref="MaxDigit" />), leaving every other digit intact.
    /// </summary>
    public static int WithDigitAtPlace(int power, int placeIndex, int newDigit)
    {
        var current = DigitAtPlace(power, placeIndex);
        return power + (newDigit - current) * PlaceValue[placeIndex];
    }

    /// <summary><c>ReturnAnimalStat</c> (int overload): the sum of all eight digits -- total invested points.</summary>
    public static int DigitSum(int power)
    {
        var sum = 0;
        for (var placeIndex = 0; placeIndex < DigitCount; placeIndex++)
            sum += DigitAtPlace(power, placeIndex);
        return sum;
    }
}
