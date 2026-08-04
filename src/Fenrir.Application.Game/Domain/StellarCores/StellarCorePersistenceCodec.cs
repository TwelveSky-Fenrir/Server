using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;

namespace Fenrir.Application.Game.Domain.StellarCores;

public static class StellarCorePersistenceCodec
{
    public const int SlotCount = StellarCoreStateResolver.SlotCount;

    private static readonly ImmutableArray<int> EmptySlots = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public static void AppendOccupiedSlots(List<CharacterStellarCoreSlotTvp> destination, int characterId,
        PlayerRuntimeState state)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var itemId = ValueAt(state.StellarCoreWardrobe, slot);
            if (itemId <= 0)
                continue;

            destination.Add(new CharacterStellarCoreSlotTvp(characterId, (byte)slot, itemId));
        }
    }

    public static ImmutableArray<int> Hydrate(IReadOnlyList<CharacterStellarCoreSlotDto> rows)
    {
        if (rows.Count == 0)
            return EmptySlots;

        var wardrobe = EmptySlots.ToBuilder();

        foreach (var row in rows)
        {
            if (row.Slot >= SlotCount)
                continue;

            wardrobe[row.Slot] = row.ItemId;
        }

        return wardrobe.ToImmutable();
    }

    public static int NormalizeIndexOnLoad(int coreIndex, ImmutableArray<int> wardrobe)
    {
        if (coreIndex < SlotCount)
            return coreIndex;

        return ValueAt(wardrobe, coreIndex % SlotCount) != 0 ? coreIndex : -1;
    }

    public static int ResolveWornNumber(int coreIndex, ImmutableArray<int> wardrobe)
    {
        return coreIndex < SlotCount ? 0 : ValueAt(wardrobe, coreIndex % SlotCount);
    }

    public static StellarCoreRentSweepResult SweepExpiredRentSlots(ImmutableArray<int> wardrobe,
        ImmutableArray<int> expireDate, int coreIndex, int today)
    {
        var wardrobeBuilder = wardrobe.IsDefaultOrEmpty ? EmptySlots.ToBuilder() : wardrobe.ToBuilder();
        var expireBuilder = expireDate.IsDefaultOrEmpty ? EmptySlots.ToBuilder() : expireDate.ToBuilder();
        var swept = false;

        for (var slot = 0; slot < SlotCount && slot < wardrobeBuilder.Count; slot++)
        {
            var itemId = wardrobeBuilder[slot];
            if (itemId == 0 || !NpcShopPolicy.IsRentItem(itemId))
                continue;

            if (slot < expireBuilder.Count && expireBuilder[slot] > today)
                continue;

            wardrobeBuilder[slot] = 0;
            if (slot < expireBuilder.Count)
                expireBuilder[slot] = 0;
            swept = true;
        }

        return swept
            ? new StellarCoreRentSweepResult(wardrobeBuilder.ToImmutable(), expireBuilder.ToImmutable(), -1, true)
            : new StellarCoreRentSweepResult(wardrobe, expireDate, coreIndex, false);
    }

    private static int ValueAt(ImmutableArray<int> slots, int slot)
    {
        return !slots.IsDefault && slot >= 0 && slot < slots.Length ? slots[slot] : 0;
    }
}

public readonly record struct StellarCoreRentSweepResult(
    ImmutableArray<int> Wardrobe,
    ImmutableArray<int> ExpireDate,
    int CoreIndex,
    bool Changed);
