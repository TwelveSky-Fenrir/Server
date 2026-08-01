using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;

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
            date[row.Slot] = row.ItemDate;
            expire[row.Slot] = row.ExpireDate;
        }

        return (wardrobe.ToImmutable(), date.ToImmutable(), expire.ToImmutable());
    }

    // Normalisation d'entree, pas une simple lecture: un index porte qui pointe un slot vide retombe a -1.
    // Server/ts25zone/S04_MyWork02.cpp:937-941 (le zone REECRIT wAvatar.aCostumeIndex a la charge).
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

    private static int ValueAt(ImmutableArray<int> slots, int slot)
    {
        return !slots.IsDefault && slot >= 0 && slot < slots.Length ? slots[slot] : 0;
    }
}
