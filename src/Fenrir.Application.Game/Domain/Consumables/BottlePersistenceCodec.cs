using System.Collections.Immutable;
using System.Globalization;

namespace Fenrir.Application.Game.Domain.Consumables;

public static class BottlePersistenceCodec
{
    public const int ItemIdDigits = 5;
    public const int CountDigits = 2;
    public const int EncodedLength = BottleResolver.SlotCount * (ItemIdDigits + CountDigits);

    private const int MaxItemId = 99999;
    private const int MinItemId = 2;
    private const int MaxCount = 99;

    private static readonly ImmutableArray<(int ItemId, int Count)> EmptySlots =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

    public static ImmutableArray<(int ItemId, int Count)> Empty => EmptySlots;

    // Largeurs et clamp EFIX_ITEM du legacy (Server/Header/CSQLAvatar.cpp:24-27,308-313).
    public static string Encode(ImmutableArray<(int ItemId, int Count)> slots)
    {
        Span<char> buffer = stackalloc char[EncodedLength];

        for (var i = 0; i < BottleResolver.SlotCount; i++)
        {
            var (itemId, count) = i < slots.Length ? slots[i] : (0, 0);

            if (itemId < MinItemId || itemId > MaxItemId)
            {
                itemId = 0;
                count = 0;
            }

            count = Math.Clamp(count, 0, MaxCount);

            var offset = i * (ItemIdDigits + CountDigits);
            itemId.TryFormat(buffer[offset..(offset + ItemIdDigits)], out _, "D5", CultureInfo.InvariantCulture);
            count.TryFormat(buffer.Slice(offset + ItemIdDigits, CountDigits), out _, "D2",
                CultureInfo.InvariantCulture);
        }

        return new string(buffer);
    }

    // Assainissement d'entree en monde du legacy : si l'un des deux est < 1, les deux passent a 0
    // (Server/ts25zone/S04_MyWork02.cpp:920-927).
    public static ImmutableArray<(int ItemId, int Count)> Decode(string? encoded)
    {
        if (encoded is null || encoded.Length < EncodedLength)
            return EmptySlots;

        var span = encoded.AsSpan();
        var builder = ImmutableArray.CreateBuilder<(int ItemId, int Count)>(BottleResolver.SlotCount);

        for (var i = 0; i < BottleResolver.SlotCount; i++)
        {
            var offset = i * (ItemIdDigits + CountDigits);

            if (!int.TryParse(span.Slice(offset, ItemIdDigits), NumberStyles.None, CultureInfo.InvariantCulture,
                    out var itemId) ||
                !int.TryParse(span.Slice(offset + ItemIdDigits, CountDigits), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var count))
            {
                itemId = 0;
                count = 0;
            }

            if (itemId < 1 || count < 1)
            {
                itemId = 0;
                count = 0;
            }

            builder.Add((itemId, count));
        }

        return builder.MoveToImmutable();
    }

    public static int SanitizeDrunkIndex(int drunkBottleIndex)
    {
        return drunkBottleIndex >= 0 && drunkBottleIndex < BottleResolver.SlotCount ? drunkBottleIndex : -1;
    }
}
