using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Characters;

// aBottle[10] (item id, 5 digits, EFIX_ITEM) et aBottleCount[10] (count, 2 digits, EFIX_NONE) sont deux colonnes
// legacy distinctes (Server/Header/CSQLAvatar.cpp:308-311) ; encodees ici cote a cote par slot dans une seule
// colonne, aux memes largeurs fixes.
public static class BottleSlotsCodec
{
    public const int SlotCount = 10;

    public const int ItemDigits = 5;

    public const int CountDigits = 2;

    public const int DigitsPerSlot = ItemDigits + CountDigits;

    public const int TextLength = SlotCount * DigitsPerSlot;

    private const int ItemModulus = 100000;

    private const int CountModulus = 100;

    public static readonly string EmptyText = new('0', TextLength);

    public static readonly ImmutableArray<(int ItemId, int Count)> Empty =
        [.. Enumerable.Repeat((0, 0), SlotCount)];

    public static string Encode(ImmutableArray<(int ItemId, int Count)> slots)
    {
        if (slots.IsDefaultOrEmpty)
            return EmptyText;

        return string.Create(TextLength, slots, static (destination, source) =>
        {
            var position = 0;

            for (var slot = 0; slot < SlotCount; slot++)
            {
                var (itemId, count) = slot < source.Length ? source[slot] : (0, 0);

                WriteValue(destination, ref position, itemId, ItemDigits, ItemModulus);
                WriteValue(destination, ref position, count, CountDigits, CountModulus);
            }
        });
    }

    public static ImmutableArray<(int ItemId, int Count)> Decode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Empty;

        var source = text.AsSpan();
        var builder = ImmutableArray.CreateBuilder<(int ItemId, int Count)>(SlotCount);

        for (var slot = 0; slot < SlotCount; slot++)
        {
            var offset = slot * DigitsPerSlot;

            builder.Add(offset + DigitsPerSlot > source.Length
                ? (0, 0)
                : (ReadValue(source.Slice(offset, ItemDigits)),
                    ReadValue(source.Slice(offset + ItemDigits, CountDigits))));
        }

        return builder.MoveToImmutable();
    }

    // CSQLAvatar.cpp:19-23 (EFIX_NONE/EFIX_ITEM ramenent le negatif a 0) puis CSQLDatabase.cpp:518 ("%0Nd").
    private static void WriteValue(Span<char> destination, ref int position, int value, int digits, int modulus)
    {
        var clamped = (value < 0 ? 0 : value) % modulus;

        for (var i = digits - 1; i >= 0; i--)
        {
            destination[position + i] = (char)('0' + (clamped % 10));
            clamped /= 10;
        }

        position += digits;
    }

    // CSQLDatabase.cpp:530-544 : atoi de la tranche, un caractere non numerique arrete la lecture.
    private static int ReadValue(ReadOnlySpan<char> digits)
    {
        var value = 0;

        foreach (var digit in digits)
        {
            if (digit is < '0' or > '9')
                break;

            value = (value * 10) + (digit - '0');
        }

        return value;
    }
}
