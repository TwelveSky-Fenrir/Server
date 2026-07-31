using System.Collections.Immutable;
using Fenrir.Domain.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory;

public readonly record struct InventoryZoneCommand(
    int CharacterId,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null,
    bool RecomputeCombatPoseAfterEquip = false);

public readonly record struct InventoryContainerSnapshot(byte Container, ImmutableDictionary<byte, ItemStack> Slots);
