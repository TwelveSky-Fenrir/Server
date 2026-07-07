using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Quests;
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
///     tSort 11's own AOI-wide AVATAR_CHANGE_INFO_1 sort-14 notice (ContributionPoints/RebirthCount/
///     Zone241Time), sent once every field above is already applied to <see cref="PlayerRuntimeState" />. Also
///     posted by the Rebirth-Pill item-consumption path (UseInventoryItemService), which never sets
///     <see cref="Zone241Time" />.
/// </param>
/// <param name="Zone241Time">
///     tSort 11 (Max Rebirth)'s own <c>aZone241Time += 10</c> side effect -- the character's new total after
///     <see cref="Fenrir.Data.Abstractions.Characters.ICharacterRepository.AdjustZone241TimeAsync" /> has
///     already durably persisted it, so applying it here (like <see cref="Tribe" />/<see cref="QuestProgress" />
///     /<see cref="TribeFourReturnAllowance" /> below) never marks <see cref="PlayerRuntimeState.MarkProgressDirty" />
///     -- see <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s own <c>ApplyTribeProgressCommand</c>
///     remarks. Never set by the Rebirth-Pill item-consumption path (Path A), which does not touch this counter.
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
/// <param name="PetActivity">
///     Paired 1:1 with <see cref="PetGrowth" /> -- see <see cref="PlayerRuntimeState.PetActivity" />
///     .
/// </param>
/// <param name="PlayTimeEvent">
///     tSort 237 (TimeExchange) -- always 0 when set (every accrued minute is consumed in full on each
///     conversion). See <see cref="PlayerRuntimeState.PlayTimeEvent" />.
/// </param>
/// <param name="EatLifePotion">Stat-potion Life/HP counter -- see <see cref="PlayerRuntimeState.EatLifePotion" />.</param>
/// <param name="EatManaPotion">Stat-potion Mana/MP counter -- see <see cref="PlayerRuntimeState.EatManaPotion" />.</param>
/// <param name="EatStrPotion">Stat-potion Strength counter -- see <see cref="PlayerRuntimeState.EatStrPotion" />.</param>
/// <param name="EatDexPotion">Stat-potion Dexterity counter -- see <see cref="PlayerRuntimeState.EatDexPotion" />.</param>
/// <param name="EatElePotion">
///     Stat-potion combined Elemental counter (already packed by the caller via
///     <see cref="Fenrir.Application.Game.Domain.Consumables.ElementalPotionPacking" /> before being set
///     here) -- see <see cref="PlayerRuntimeState.EatElePotion" />.
/// </param>
/// <param name="FullActionRebroadcast">
///     Op 23 (CZ_USE_INVENTORY_ITEM_SEND) stat-potion family's own post-consumption self + AOI-neighbor
///     avatar-action refresh (Server/ts25zone/S04_MyWork03.cpp:3188-3211) -- same <c>SendAvatarAction</c>/
///     <c>BroadcastAvatarAction</c> pairing <see cref="CostumeZoneCommand.FullActionRebroadcast" /> already
///     uses for its own op 139 branch, reused here rather than duplicated. Legacy sends the self-refresh
///     twice in a row ("to fix live client display without requiring a relog"); Fenrir sends it once -- a
///     second identical, immutable snapshot cannot convey information a first send didn't already, so the
///     double-send is treated as a legacy redundancy artifact, not reproduced verbatim.
/// </param>
/// <param name="PetExpX2Time">
///     Pet EXP boost pill (world.Items 1190/17035/8413) -- the character's new running duration-counter
///     total after this use. See <see cref="PlayerRuntimeState.PetExpX2Time" />.
/// </param>
/// <param name="Tribe">
///     CZ_CHANGE_TO_TRIBE4_SEND (op37) success -- the character's new tribe membership. Already synchronously
///     persisted (game.usp_Character_ApplyTribeFourConversion) by the time this is posted, so applying it here
///     never marks <see cref="PlayerRuntimeState.MarkProgressDirty" /> the way most other fields on this
///     record do -- see <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s own
///     <c>ApplyTribeProgressCommand</c> remarks.
/// </param>
/// <param name="QuestProgress">
///     CZ_CHANGE_TO_TRIBE4_SEND (op37) success -- the character's 5-slot quest state after conversion (reset
///     to idle on the outbound branch, or the restored tribe's terminal/complete step on the return branch).
///     Same "already synchronously persisted, no dirty mark" posture as <see cref="Tribe" />.
/// </param>
/// <param name="TribeFourReturnAllowance">
///     CZ_CHANGE_TO_TRIBE4_SEND (op37) return-branch success -- the character's new
///     <see cref="PlayerRuntimeState.TribeFourReturnAllowance" /> total (decremented by one). Session-scoped
///     only (see that field's own remarks), so there is nothing to persist and no dirty mark either.
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
    int? Zone241Time = null,
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
    int? EatLifePotion = null,
    int? EatManaPotion = null,
    int? EatStrPotion = null,
    int? EatDexPotion = null,
    int? EatElePotion = null,
    bool FullActionRebroadcast = false,
    int? PetExpX2Time = null,
    byte? Tribe = null,
    QuestProgress? QuestProgress = null,
    int? TribeFourReturnAllowance = null,
    TaskCompletionSource? Applied = null);

/// <summary>One ground-item drop request -- see TribeProgressZoneCommand.DropItems.</summary>
public readonly record struct TribeGroundItemDrop(int ItemId, int Quantity);
