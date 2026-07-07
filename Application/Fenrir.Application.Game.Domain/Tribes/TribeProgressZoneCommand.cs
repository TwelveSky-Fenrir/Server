using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Tribes;

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
/// <param name="TribeNotifyScrollCount">
///     CZ_TRIBE_NOTIFY_SEND (opcode 112) -- the sender's remaining scroll count after
///     this send, mirroring <see cref="PlayerRuntimeState.TribeNotifyScrollCount" />.
/// </param>
/// <param name="Exp2">tSort 11 (Max Rebirth) resets this to 0 on success -- see <see cref="RebirthCount" />.</param>
/// <param name="RebirthCount">tSort 11 (Max Rebirth) increments this by 1 on success.</param>
/// <param name="RebirthBroadcast">
///     tSort 11's own AOI-wide AVATAR_CHANGE_INFO_1 sort-14 notice (ContributionPoints/RebirthCount), sent once
///     both fields above are already applied to <see cref="PlayerRuntimeState" />.
/// </param>
/// <param name="LodRounds">
///     Item 1434's banked "Life or Death" round counter -- see
///     <see cref="PlayerRuntimeState.LodRounds" />.
/// </param>
/// <param name="ProtectForRefine">Preserve Charm -- see <see cref="PlayerRuntimeState.ProtectForRefine" />.</param>
/// <param name="ProtectForDestroy">Protection Charm -- see <see cref="PlayerRuntimeState.ProtectForDestroy" />.</param>
/// <param name="ProtectForCostume">Guardian Charm -- see <see cref="PlayerRuntimeState.ProtectForCostume" />.</param>
/// <param name="ProtectForDestroy2">Absolute Craft Ticket -- see <see cref="PlayerRuntimeState.ProtectForDestroy2" />.</param>
/// <param name="ImproveItemValue">Lucky Enchant Scroll -- see <see cref="PlayerRuntimeState.ImproveItemValue" />.</param>
/// <param name="AddItemValue">Lucky Combine Scroll -- see <see cref="PlayerRuntimeState.AddItemValue" />.</param>
/// <param name="HighItemValue">Lucky Upgrade Scroll -- see <see cref="PlayerRuntimeState.HighItemValue" />.</param>
/// <param name="DropItemTime">Lucky Drop Scroll -- see <see cref="PlayerRuntimeState.DropItemTime" />.</param>
/// <param name="TaiyanKeyTimer">Taiyan Key -- see <see cref="PlayerRuntimeState.TaiyanKeyTimer" />.</param>
/// <param name="TeacherPoint">
///     tSort 237 (TimeExchange) -- the character's new total after granting 694 points per accrued
///     play-time-event minute. See <see cref="PlayerRuntimeState.TeacherPoint" />.
/// </param>
/// <param name="PetGrowth">
///     tSort 237 (TimeExchange)'s pet-experience credit -- only set when
///     <c>PetExperienceCreditResolver.Resolve</c> found an eligible equipped pet with something to change
///     (credited amount &gt; 0 or a reactivation), same guard <c>Zone.CreditPetGrowthFromMonsterKill</c>
///     already applies for its own (unrelated) call site. See <see cref="PlayerRuntimeState.PetGrowth" />.
/// </param>
/// <param name="PetActivity">Paired 1:1 with <see cref="PetGrowth" /> -- see <see cref="PlayerRuntimeState.PetActivity" />.</param>
/// <param name="PlayTimeEvent">
///     tSort 237 (TimeExchange) -- always 0 when set (every accrued minute is consumed in full on each
///     conversion). See <see cref="PlayerRuntimeState.PlayTimeEvent" />.
/// </param>
/// <param name="Applied">
///     Completed once actually mirrored -- see InventoryZoneCommand.Applied for why this matters while
///     EconomyActionLock is held.
/// </param>
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
    int? TribeNotifyScrollCount = null,
    int? Exp2 = null,
    int? RebirthCount = null,
    bool RebirthBroadcast = false,
    int? LodRounds = null,
    int? ProtectForRefine = null,
    int? ProtectForDestroy = null,
    int? ProtectForCostume = null,
    int? ProtectForDestroy2 = null,
    int? ImproveItemValue = null,
    int? AddItemValue = null,
    int? HighItemValue = null,
    int? DropItemTime = null,
    int? TaiyanKeyTimer = null,
    int? TeacherPoint = null,
    int? PetGrowth = null,
    byte? PetActivity = null,
    int? PlayTimeEvent = null,
    TaskCompletionSource? Applied = null);

/// <summary>One ground-item drop request -- see TribeProgressZoneCommand.DropItems.</summary>
public readonly record struct TribeGroundItemDrop(int ItemId, int Quantity);
