using System.Collections.Immutable;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Posted by an async inventory handler (<c>GenericActionHandler</c>) AFTER it has already validated
///     the move (<see cref="ContainerMatrix" />), computed fresh stats if needed
///     (<see cref="EquipmentService" />), and durably persisted the affected container(s) via
///     <c>CharacterRepository.ReplaceContainerAsync</c> (D7 regime (b), synchronous flush -- same posture as
///     money). This command carries ONLY the already-decided, already-durable result: <c>Zone</c>'s own tick
///     (the single mutator of <c>PlayerRuntimeState</c>) just mirrors it into
///     <c>PlayerRuntimeState.Inventory</c>/<c>PlayerRuntimeState.Stats</c> -- no validation, no I/O, no
///     business logic happens on the receiving end, which is what lets this run on the zone's own tick thread
///     without blocking every other player in the zone on a SQL round trip.
/// </summary>
/// <param name="CharacterId">Which player this command applies to -- a no-op if they already left the zone by the time the tick drains this.</param>
/// <param name="Containers">
///     One entry per container touched by the move (in practice always 1 -- same-container moves -- or 2 for
///     this task's implemented tSort families): the FULL new content of that container, mirroring the same
///     "whole-container replace" semantics <c>usp_CharacterItems_ReplaceContainer</c> already uses, so the
///     in-memory cache and the DB can never structurally disagree about what a "container" even means.
/// </param>
/// <param name="UpdatedStats">
///     Non-null only when the move touched <see cref="ContainerMatrix.Equipment" /> -- the freshly recomputed
///     <see cref="EffectiveStats" /> (<see cref="EquipmentService.RecomputeStats" />), replacing
///     <c>PlayerRuntimeState.Stats</c> wholesale exactly like every other equipment-change event.
/// </param>
/// <param name="Applied">
///     Completed by <c>Zone.ApplyInventoryCommand</c> the instant the tick actually mirrors this command into
///     <c>PlayerRuntimeState.Inventory</c> (whether that mirror finds the player still present or not) --
///     NOT merely when this command is posted/accepted into the inbox. Review finding (Phase C/V5 NPC &amp;
///     Economy): posting alone is not enough to close the read-await-write race the per-character
///     <c>PlayerRuntimeState.EconomyActionLock</c> exists to serialize -- a handler that released that lock
///     right after posting (rather than after the mirror is CONFIRMED applied) would still let a
///     back-to-back second request read a stale, pre-mirror <c>Inventory</c> snapshot if it arrived before
///     the zone's next tick. Null for any caller that genuinely doesn't need this guarantee (there are none
///     left in this codebase as of this fix, but the field stays optional rather than required so a future,
///     provably-safe caller isn't forced to pay for a signal it doesn't need).
/// </param>
public readonly record struct InventoryZoneCommand(
    int CharacterId,
    ImmutableArray<InventoryContainerSnapshot> Containers,
    EffectiveStats? UpdatedStats,
    TaskCompletionSource? Applied = null);

/// <summary>One touched container's full new content, keyed by <see cref="ContainerMatrix" />'s Container byte.</summary>
public readonly record struct InventoryContainerSnapshot(byte Container, ImmutableDictionary<byte, ItemStack> Slots);
