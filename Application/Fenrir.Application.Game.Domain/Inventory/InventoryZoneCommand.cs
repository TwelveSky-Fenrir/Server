using System.Collections.Immutable;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Posted by an async inventory handler after it has already validated the move, computed fresh stats if
///     needed, and durably persisted the affected container(s). Zone's tick just mirrors this already-decided
///     result into PlayerRuntimeState -- no validation, no I/O, so it never blocks the zone tick on SQL.
/// </summary>
/// <param name="CharacterId">A no-op if the player already left the zone by the time the tick drains this.</param>
/// <param name="Containers">One entry per touched container: its full new content (whole-container replace).</param>
/// <param name="UpdatedStats">Non-null only when the move touched Equipment.</param>
/// <param name="Applied">
///     Completed the instant the tick actually mirrors this into PlayerRuntimeState.Inventory, not merely when
///     posted -- callers serializing on PlayerRuntimeState.EconomyActionLock must await this before releasing
///     the lock, or a back-to-back request can read a stale pre-mirror snapshot.
/// </param>
public readonly record struct InventoryZoneCommand(
    int CharacterId,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null);

/// <summary>One touched container's full new content, keyed by ContainerMatrix's Container byte.</summary>
public readonly record struct InventoryContainerSnapshot(byte Container, ImmutableDictionary<byte, ItemStack> Slots);
