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
/// <param name="MaxLife">
///     The GM "Basic"-tier LEVEL self-command's (tSort 521) own recomputed-stats cache write, mirroring
///     <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s own <c>ApplyCharacterExperienceGain</c>
///     level-up cascade (<see cref="PlayerRuntimeState.MaxLife" /> set alongside, not derived from,
///     <see cref="UpdatedStats" />). Paired 1:1 with <see cref="Life" /> for that one caller.
/// </param>
/// <param name="MaxMana">Companion to <see cref="MaxLife" /> -- see <see cref="PlayerRuntimeState.MaxMana" />.</param>
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
/// <param name="EliteDungeonTime">
///     Elite Dungeon Ticket family (1047/1097/1098) -- see
///     <see cref="PlayerRuntimeState.EliteDungeonTime" />.
/// </param>
/// <param name="DungeonKeyTime">Item 1048 -- see <see cref="PlayerRuntimeState.DungeonKeyTime" />.</param>
/// <param name="IvyHallTicketTime">
///     Ivy Hall Ticket pair (553/1219) -- see
///     <see cref="PlayerRuntimeState.IvyHallTicketTime" />.
/// </param>
/// <param name="ScrollOfSeekersTime">
///     Scroll of Seekers family (1124/1187/7016/8409/8410) -- see
///     <see cref="PlayerRuntimeState.ScrollOfSeekersTime" />.
/// </param>
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
/// <param name="StoreMoney">
///     CZ_PROCESS_DATA_SEND tSort 226/227 (Store/coffre money deposit/withdraw) -- the character's new
///     Store-money total after
///     <see cref="Fenrir.Data.Abstractions.Characters.ICharacterRepository.AdjustStoreMoneyAsync" />
///     has already durably persisted it, so applying it here (like <see cref="Zone241Time" />/
///     <see cref="Tribe" />/<see cref="QuestProgress" /> above) never marks
///     <see cref="PlayerRuntimeState.MarkProgressDirty" /> -- see
///     <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s own <c>ApplyTribeProgressCommand</c> remarks.
/// </param>
/// <param name="SkillPoints">
///     The Admin-tier (<c>GmCommandTier.Admin</c>) MAX stat-cheat (CZ_PROCESS_DATA_SEND tSort 509) forces
///     this to a fixed 3000 -- see <see cref="Fenrir.Application.Game.Services.Gm.GmMaxStatService" />. No
///     other caller sets this field today; the ordinary learn-skill/upgrade-skill SkillPoints debit instead
///     goes through the separate, slot-paired <see cref="Fenrir.Application.Game.Domain.Skills.SkillZoneCommand" />
///     channel, which this field does not replace.
/// </param>
/// <param name="GmSummonMonsterTemplateId">
///     The Elevated-tier (<c>GmCommandTier.Elevated</c>) "moncall" GM command (CZ_PROCESS_DATA_SEND tSort 506)
///     -- the requested monster template id, spawned at the invoking GM's own current position
///     (<see cref="Fenrir.Application.Game.Services.Gm.GmSummonMonsterService" />). Silently a no-op if the id
///     does not resolve in <see cref="Fenrir.Application.Game.GameData.WorldDataCache.MonstersById" /> or if
///     Fenrir's own GM-summon slot pool is exhausted (<see cref="Fenrir.Application.Game.Domain.World.Zone" />'s
///     own <c>SpawnGmSummonedMonster</c> remarks) -- matching legacy's own "not validated, whatever the spawn
///     primitive does with an invalid id is not surfaced back to the GM" behavior.
/// </param>
/// <param name="VisibleState">
///     The GM "Basic"-tier HIDE(tSort 501, value 0)/SHOW(tSort 502, value 1) self-commands -- see
///     <see cref="PlayerRuntimeState.VisibleState" />.
/// </param>
/// <param name="SpecialState">
///     The GM "Basic"-tier EQUIP(tSort 511, value 1)/UNEQUIP(tSort 512, value 0) self-commands, and the
///     NCHAT(tSort 516, value 2)/YCHAT(tSort 517, value 0) by-name-target commands (posted onto the TARGET's
///     own zone, not the invoking GM's) -- see <see cref="PlayerRuntimeState.SpecialState" />.
/// </param>
/// <param name="PreviousTribe">
///     The GM "Basic"-tier TRIBE self-command (tSort 510) when the requested selector is not the special
///     value 3 -- mirrors the selector onto <see cref="PlayerRuntimeState.PreviousTribe" /> alongside
///     <see cref="Tribe" />. Unlike <see cref="Tribe" />/<see cref="QuestProgress" />, this is NOT already
///     synchronously persisted by any existing repository call (see
///     <see cref="Fenrir.Application.Game.Services.Gm.IGmBasicCommandService" />'s own remarks for the
///     flagged persistence gap this leaves) -- still deliberately excluded from the generic
///     <c>changed</c>/<see cref="DirtyFlags.Progression" /> bucket below anyway, matching <see cref="Tribe" />'s
///     own posture, since Tribe/PreviousTribe are not columns the Progression write-behind flush covers
///     regardless.
/// </param>
/// <param name="TeleportTo">
///     Unconditional absolute position overwrite (no bounds/map-validity/collision check) plus the matching
///     <see cref="AoiGrid" /> cell refresh -- the GM "Basic"-tier self-teleport-to-coordinate (tSort 507),
///     CALL's target-warped-to-invoker branch (tSort 514), and MOVE-to-target's invoker-warped-to-target
///     branch (tSort 515) all funnel through this one field. Always marks <see cref="DirtyFlags.Position" />
///     (never folded into the generic <c>changed</c>/<see cref="DirtyFlags.Progression" /> bucket below),
///     matching <see cref="Fenrir.Application.Game.Domain.World.Zone.PlayerLifecycle" />'s own
///     <c>HandleMove</c> convention for every other position write in this codebase.
/// </param>
/// <param name="NeighborActionBroadcast">
///     CALL's (tSort 514) own "general action/notification broadcast... so nearby players observe the
///     target's sudden relocation" side effect -- an AOI-neighbor-only <c>BroadcastAvatarAction</c> (no
///     self-send, unlike <see cref="FullActionRebroadcast" />) issued around the character's position AFTER
///     <see cref="TeleportTo" /> (if also set) has already been applied. MOVE-to-target's (tSort 515) own
///     contract explicitly states no such broadcast is issued for that sibling command, so this is never set
///     alongside a caller-side <see cref="TeleportTo" /> for that one.
/// </param>
/// <param name="Level">
///     The GM "Basic"-tier LEVEL self-command (tSort 521) -- see
///     <see cref="Fenrir.Application.Game.Services.Gm.IGmBasicCommandService" />.
/// </param>
/// <param name="Level2">Companion to <see cref="Level" /> -- see <see cref="PlayerRuntimeState.Level2" />.</param>
/// <param name="Experience">Companion to <see cref="Level" /> -- see <see cref="PlayerRuntimeState.Experience" />.</param>
/// <param name="M15PetLuckyBoxPity">
///     The M15 Pet Lucky Box (world.Items 8111) pity counter -- <c>LootBoxUseItemHandler</c> posts this after
///     every box-8111 open (the reward-id-override closure has already written the same value directly onto
///     <see cref="PlayerRuntimeState.M15PetLuckyBoxPity" /> synchronously; this mirror exists purely so
///     applying it here marks <see cref="DirtyFlags" />.Progression dirty, since that tracker is Zone-internal
///     state a handler cannot reach directly). See <see cref="PlayerRuntimeState.M15PetLuckyBoxPity" />'s own
///     remarks for the confirmation-pass persistence gap this closes.
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
    int? MaxLife = null,
    int? MaxMana = null,
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
    int? EliteDungeonTime = null,
    int? DungeonKeyTime = null,
    int? IvyHallTicketTime = null,
    int? ScrollOfSeekersTime = null,
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
    long? StoreMoney = null,
    int? SkillPoints = null,
    int? GmSummonMonsterTemplateId = null,
    int? VisibleState = null,
    int? SpecialState = null,
    byte? PreviousTribe = null,
    (float X, float Y, float Z)? TeleportTo = null,
    bool NeighborActionBroadcast = false,
    short? Level = null,
    short? Level2 = null,
    long? Experience = null,
    int? M15PetLuckyBoxPity = null,
    TaskCompletionSource? Applied = null);

/// <summary>
///     One ground-item drop request -- see TribeProgressZoneCommand.DropItems.
///     <paramref name="DropSort" /> defaults to 0 (the pre-existing, generic drop-source code every caller
///     before the Admin-tier GM spawn-item command used implicitly) so no existing caller's behavior changes;
///     <see cref="Fenrir.Application.Game.Services.Gm.GmCreateItemService" /> is the one caller that supplies
///     a non-default value (<see cref="Fenrir.Application.Game.Domain.World.Loot.GroundItemEntity.GmCreateItemDropSort" />
///     ).
/// </summary>
public readonly record struct TribeGroundItemDrop(int ItemId, int Quantity, int DropSort = 0);
