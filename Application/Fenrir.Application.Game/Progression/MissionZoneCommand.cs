using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;

namespace Fenrir.Application.Game.Progression;

/// <summary>
///     Posted after a daily-mission claim is already durably persisted; Zone's tick just mirrors the counters
///     and reward deposit onto the live PlayerRuntimeState/Inventory.
/// </summary>
public readonly record struct MissionZoneCommand(
    int CharacterId,
    int JoinWar,
    int KillOtherTribe,
    int KillMonster,
    int PlayTime,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    TaskCompletionSource? Applied = null);
