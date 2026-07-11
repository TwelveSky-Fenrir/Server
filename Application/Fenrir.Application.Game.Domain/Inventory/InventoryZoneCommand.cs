using System.Collections.Immutable;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory;

public readonly record struct InventoryZoneCommand(
    int CharacterId,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null);

public readonly record struct InventoryContainerSnapshot(byte Container, ImmutableDictionary<byte, ItemStack> Slots);
