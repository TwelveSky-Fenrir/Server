using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Characters;

public static class StellarCoreExpireDateCodec
{
    public const int SlotCount = 10;

    public const int DigitsPerSlot = 8;

    public const int TextLength = SlotCount * DigitsPerSlot;

    private const int LowerBound = 0;

    private const int UpperBound = 99999999;

    public static readonly string EmptyText = new('0', TextLength);

    public static readonly ImmutableArray<int> Empty = [.. Enumerable.Repeat(0, SlotCount)];

    public static string Encode(ImmutableArray<int> slots)
    {
        if (slots.IsDefaultOrEmpty)
            return EmptyText;

        return string.Create(TextLength, slots, static (destination, source) =>
        {
            var position = 0;

            for (var slot = 0; slot < SlotCount; slot++)
            {
                var value = slot < source.Length ? source[slot] : 0;
                WriteValue(destination, ref position, value);
            }
        });
    }

    public static ImmutableArray<int> Decode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Empty;

        var source = text.AsSpan();
        var builder = ImmutableArray.CreateBuilder<int>(SlotCount);

        for (var slot = 0; slot < SlotCount; slot++)
        {
            var offset = slot * DigitsPerSlot;
            builder.Add(offset + DigitsPerSlot > source.Length ? 0 : ReadValue(source.Slice(offset, DigitsPerSlot)));
        }

        return builder.MoveToImmutable();
    }

    private static void WriteValue(Span<char> destination, ref int position, int value)
    {
        var clamped = value is < LowerBound or > UpperBound ? 0 : value;

        for (var i = DigitsPerSlot - 1; i >= 0; i--)
        {
            destination[position + i] = (char)('0' + clamped % 10);
            clamped /= 10;
        }

        position += DigitsPerSlot;
    }

    private static int ReadValue(ReadOnlySpan<char> digits)
    {
        var value = 0;

        foreach (var digit in digits)
        {
            if (digit is < '0' or > '9')
                break;

            value = value * 10 + (digit - '0');
        }

        return value;
    }
}
