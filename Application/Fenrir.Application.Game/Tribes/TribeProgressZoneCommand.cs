using System.Collections.Immutable;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tribes;

/// <summary>
///     Posted after a TRIBE_WORK self-mutation is already decided, to mirror it onto the actor's own
///     PlayerRuntimeState. Every field is nullable/optional and independently applied; null means "this
///     tSort did not touch this field," not "reset to zero".
/// </summary>
/// <param name="CharacterId">A no-op if the player already left the zone.</param>
/// <param name="TribeRole">
///     TRIBE_WORK tSort 2 (appoint, -&gt; 2) / tSort 3 (remove, -&gt; 0) mirror this onto the sub-master
///     TARGET, not the requesting Force Leader, so that character's next role-gated action sees the change
///     without a re-log.
/// </param>
/// <param name="Life">tSort 1 forces this to 1, matching the legacy's own aLifeValue=1.</param>
/// <param name="Mana">tSort 1 forces this to 0, matching the legacy's own aManaValue=0.</param>
/// <param name="UpdatedStats">Freshly recomputed whenever a stat-affecting field above changed; null otherwise.</param>
/// <param name="DropItems">
///     Ground-item drop(s) to spawn at the character's current position when applied. Fenrir's ground-item
///     pool has no capacity limit (unlike the legacy's ProcessForDropItem), so that failure path is
///     unreachable here.
/// </param>
/// <param name="Applied">Completed once actually mirrored -- see InventoryZoneCommand.Applied for why this matters while EconomyActionLock is held.</param>
public readonly record struct TribeProgressZoneCommand(
    int CharacterId,
    int? ContributionPoints = null,
    byte? TribeRole = null,
    int? Title = null,
    int? Halo = null,
    int? ProtectForHalo = null,
    bool? UseOrnament = null,
    int? BonusItemLevel = null,
    bool? BonusItemValue = null,
    int? StatVit = null,
    int? StatStr = null,
    int? StatInt = null,
    int? StatDex = null,
    int? StatPoints = null,
    int? Life = null,
    int? Mana = null,
    EffectiveStats? UpdatedStats = null,
    ImmutableArray<TribeGroundItemDrop> DropItems = default,
    TaskCompletionSource? Applied = null);

/// <summary>One ground-item drop request -- see TribeProgressZoneCommand.DropItems.</summary>
public readonly record struct TribeGroundItemDrop(int ItemId, int Quantity);
