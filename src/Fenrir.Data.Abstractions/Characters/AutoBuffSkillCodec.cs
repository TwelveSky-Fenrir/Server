using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Characters;

public static class AutoBuffSkillCodec
{
    public const int SlotCount = 8;

    public const int ValuesPerSlot = 2;

    public const int DigitsPerValue = 3;

    public const int TextLength = SlotCount * ValuesPerSlot * DigitsPerValue;

    private const int ValueModulus = 1000;

    public static readonly string EmptyText = new('0', TextLength);

    public static readonly ImmutableArray<(int SkillId, int Grade)> Empty =
        [.. Enumerable.Repeat((0, 0), SlotCount)];

    public static string Encode(ImmutableArray<(int SkillId, int Grade)> slots)
    {
        if (slots.IsDefaultOrEmpty)
            return EmptyText;

        return string.Create(TextLength, slots, static (destination, source) =>
        {
            var position = 0;

            for (var slot = 0; slot < SlotCount; slot++)
            {
                var (skillId, grade) = slot < source.Length ? source[slot] : (0, 0);

                WriteValue(destination, ref position, skillId);
                WriteValue(destination, ref position, grade);
            }
        });
    }

    public static ImmutableArray<(int SkillId, int Grade)> Decode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Empty;

        var source = text.AsSpan();
        var builder = ImmutableArray.CreateBuilder<(int SkillId, int Grade)>(SlotCount);

        for (var slot = 0; slot < SlotCount; slot++)
        {
            var offset = slot * ValuesPerSlot * DigitsPerValue;

            builder.Add(offset + ValuesPerSlot * DigitsPerValue > source.Length
                ? (0, 0)
                : (ReadValue(source.Slice(offset, DigitsPerValue)),
                    ReadValue(source.Slice(offset + DigitsPerValue, DigitsPerValue))));
        }

        return builder.MoveToImmutable();
    }

    private static void WriteValue(Span<char> destination, ref int position, int value)
    {
        var clamped = (value < 0 ? 0 : value) % ValueModulus;

        destination[position] = (char)('0' + clamped / 100);
        destination[position + 1] = (char)('0' + clamped / 10 % 10);
        destination[position + 2] = (char)('0' + clamped % 10);
        position += DigitsPerValue;
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
