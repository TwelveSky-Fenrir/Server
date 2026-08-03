using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Mounts;

public static class MountPersistenceCodec
{
    public const int PersistedGarageSlot = 0;

    public const int SlotCount = MountStateResolver.SlotCount;

    public static bool IsMounted(int animalIndex)
    {
        return animalIndex >= MountAnimalInfo.ActiveCompanionSlotBase &&
               animalIndex < MountAnimalInfo.ActiveCompanionSlotBase + MountAnimalInfo.ActiveCompanionSlotCount;
    }

    public static int EncodeExpActivity(ImmutableArray<int> mountActivity, ImmutableArray<int> mountAccumulatedExp)
    {
        return MountActivityExpCodec.Pack(mountActivity[PersistedGarageSlot],
            mountAccumulatedExp[PersistedGarageSlot]);
    }

    public static int EncodePower(ImmutableArray<int> mountRolledAttributes)
    {
        return MountPowerCodec.EncodeSlot(mountRolledAttributes, PersistedGarageSlot);
    }

    public static void AppendOccupiedSlots(List<CharacterMountSlotTvp> destination, int characterId,
        PlayerRuntimeState state)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var itemId = ValueAt(state.MountGarage, slot);
            if (itemId <= 0)
                continue;

            destination.Add(new CharacterMountSlotTvp(characterId, (byte)slot, itemId,
                MountActivityExpCodec.Pack(ValueAt(state.MountActivity, slot),
                    ValueAt(state.MountAccumulatedExp, slot)),
                MountPowerCodec.EncodeSlot(state.MountRolledAttributes, slot)));
        }
    }

    public static ImmutableArray<(int ItemId, int ExpActivity, int Power)> Hydrate(
        IReadOnlyList<CharacterMountSlotDto> rows)
    {
        var slots = EmptySlots();

        foreach (var row in rows)
        {
            if (row.Slot >= SlotCount)
                continue;

            slots[row.Slot] = (row.ItemId, row.ExpActivity, row.Power);
        }

        return slots.MoveToImmutable();
    }

    public static ImmutableArray<(int ItemId, int ExpActivity, int Power)> SingleSlot(int itemId, int expActivity,
        int power)
    {
        var slots = EmptySlots();
        slots[PersistedGarageSlot] = (itemId, expActivity, power);
        return slots.MoveToImmutable();
    }

    private static ImmutableArray<(int ItemId, int ExpActivity, int Power)>.Builder EmptySlots()
    {
        var builder = ImmutableArray.CreateBuilder<(int ItemId, int ExpActivity, int Power)>(SlotCount);
        for (var slot = 0; slot < SlotCount; slot++)
            builder.Add((0, 0, 0));
        return builder;
    }

    private static int ValueAt(ImmutableArray<int> slots, int slot)
    {
        return !slots.IsDefault && slot >= 0 && slot < slots.Length ? slots[slot] : 0;
    }
}
