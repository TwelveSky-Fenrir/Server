using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Npcs;

namespace Fenrir.Application.Game.Domain.Costumes;

public static class CostumePersistenceCodec
{
    public const int SlotCount = CostumeStateResolver.SlotCount;

    private static readonly ImmutableArray<int> EmptySlots = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public static void AppendOccupiedSlots(List<CharacterCostumeSlotTvp> destination, int characterId,
        PlayerRuntimeState state)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var itemId = ValueAt(state.CostumeWardrobe, slot);
            if (itemId == 0)
                continue;

            destination.Add(new CharacterCostumeSlotTvp(characterId, (byte)slot, itemId,
                ValueAt(state.CostumeDate, slot), ValueAt(state.CostumeExpireDate, slot)));
        }
    }

    public static (ImmutableArray<int> Wardrobe, ImmutableArray<int> Date, ImmutableArray<int> ExpireDate) Hydrate(
        IReadOnlyList<CharacterCostumeSlotDto> rows)
    {
        if (rows.Count == 0)
            return (EmptySlots, EmptySlots, EmptySlots);

        var wardrobe = EmptySlots.ToBuilder();
        var date = EmptySlots.ToBuilder();
        var expire = EmptySlots.ToBuilder();

        foreach (var row in rows)
        {
            if (row.Slot >= SlotCount)
                continue;

            wardrobe[row.Slot] = row.ItemId;
            date[row.Slot] = row.ItemValue;
            expire[row.Slot] = row.ExpireDate;
        }

        return (wardrobe.ToImmutable(), date.ToImmutable(), expire.ToImmutable());
    }

    public static int NormalizeIndexOnLoad(int costumeIndex, ImmutableArray<int> wardrobe)
    {
        if (costumeIndex < SlotCount)
            return costumeIndex;

        return ValueAt(wardrobe, costumeIndex % SlotCount) != 0 ? costumeIndex : -1;
    }

    public static int ResolveWornNumber(int costumeIndex, ImmutableArray<int> wardrobe)
    {
        return costumeIndex < SlotCount ? 0 : ValueAt(wardrobe, costumeIndex % SlotCount);
    }

    public static CostumeRentSweepResult SweepExpiredRentSlots(ImmutableArray<int> wardrobe, ImmutableArray<int> date,
        ImmutableArray<int> expireDate, int costumeIndex, int today)
    {
        var wardrobeBuilder = wardrobe.IsDefaultOrEmpty ? EmptySlots.ToBuilder() : wardrobe.ToBuilder();
        var dateBuilder = date.IsDefaultOrEmpty ? EmptySlots.ToBuilder() : date.ToBuilder();
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
            if (slot < dateBuilder.Count)
                dateBuilder[slot] = 0;
            if (slot < expireBuilder.Count)
                expireBuilder[slot] = 0;
            swept = true;
        }

        return swept
            ? new CostumeRentSweepResult(wardrobeBuilder.ToImmutable(), dateBuilder.ToImmutable(),
                expireBuilder.ToImmutable(), -1, true)
            : new CostumeRentSweepResult(wardrobe, date, expireDate, costumeIndex, false);
    }

    private static int ValueAt(ImmutableArray<int> slots, int slot)
    {
        return !slots.IsDefault && slot >= 0 && slot < slots.Length ? slots[slot] : 0;
    }
}

public readonly record struct CostumeRentSweepResult(
    ImmutableArray<int> Wardrobe,
    ImmutableArray<int> Date,
    ImmutableArray<int> ExpireDate,
    int CostumeIndex,
    bool Changed);
