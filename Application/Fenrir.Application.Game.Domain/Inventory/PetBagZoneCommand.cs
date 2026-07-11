using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

public readonly record struct PetBagSlotWrite(byte Slot, int? ItemId);

public readonly record struct PetBagZoneCommand(
    int CharacterId,
    ImmutableArray<PetBagSlotWrite> PetBagWrites,
    InventoryContainerSnapshot? InventoryContainer,
    TaskCompletionSource? Applied = null);
