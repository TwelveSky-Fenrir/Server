using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.World;

/// <summary>
///     A player's in-memory, authoritative state while <c>InWorld</c>. Mutated ONLY by <see cref="Zone.RunAsync" />
///     (architecture reference §10.1: "un seul écrivain") — every other thread that needs a player's state posts a
///     <see cref="ZoneCommand" /> and waits for the next tick instead of touching this directly.
/// </summary>
public sealed class PlayerRuntimeState
{
    public required int CharacterId { get; init; }
    public required IPacketSession Session { get; init; }
    public required string Name { get; init; }
    public required byte Tribe { get; init; }
    public required byte Gender { get; init; }
    public required byte HeadType { get; init; }
    public required byte FaceType { get; init; }
    public required short Level { get; init; }

    public short MapId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float Heading { get; set; }
    public int Life { get; set; }
    public int MaxLife { get; set; }
    public int Mana { get; set; }
    public int MaxMana { get; set; }

    /// <summary>
    ///     Spent base stat points (game.Characters.StatVit/StatStr/StatInt/StatDex, A3/A4 migration; legacy
    ///     aVit/aStr/aInt/aDex). Feed <see cref="StatCalculator.ComputeBaseStats" />/
    ///     <see cref="StatCalculator.ComputeEffectiveStats" /> directly as
    ///     <see cref="CharacterBaseAttributes" />.Vitality/Strength/Intelligence/Dexterity (report 11 §3) —
    ///     StatInt IS the report's "Ki", StatDex IS the report's "Wisdom", see that type's remarks.
    /// </summary>
    public int StatVit { get; set; }

    public int StatStr { get; set; }
    public int StatInt { get; set; }
    public int StatDex { get; set; }

    /// <summary>Unspent stat/skill points (aStatPoint/aSkillPoint) — spend-on-levelup UI reads these, not StatCalculator.</summary>
    public int StatPoints { get; set; }

    public int SkillPoints { get; set; }

    /// <summary>
    ///     Learned-skill slots (legacy <c>aSkill[40][0..1]</c>, game.CharacterSkills -- a slot simply absent
    ///     from this dictionary IS "empty", same normalization convention as <see cref="Inventory" />'s own
    ///     containers). Mutated ONLY by <see cref="Zone" />'s own tick: seeded at world entry
    ///     (<c>Zone.HandleEnter</c> from the A3 world-entry bundle's skill result set / an in-process handoff's
    ///     carried-over snapshot), replaced slot-by-slot by <c>Zone.ApplySkillCommand</c> for every later
    ///     accepted learn (tSort 202/233) or upgrade (tSort 203). A plain field-initialized empty dictionary
    ///     (not <c>required</c>): a brand-new character with no skills yet is a perfectly valid starting state.
    /// </summary>
    public ImmutableDictionary<byte, LearnedSkill> LearnedSkills { get; set; } = ImmutableDictionary<byte, LearnedSkill>.Empty;

    /// <summary>Total XP (aExp1/aExp2 combined, game.Characters.Experience — see that column's migration comment).</summary>
    public long Experience { get; set; }

    /// <summary>
    ///     aRebirthNum (MAX_REBIRTH_LIMIT=12) — read by StatCalculator's CriticalDefence base bonus (report §5.5)
    ///     and Critical wrapper bonus (report §6), both via <see cref="CharacterBaseAttributes.RebirthCount" />.
    /// </summary>
    public int RebirthCount { get; set; }

    /// <summary>
    ///     aTitle (category*100 + rank 1-14) — read by StatCalculator's title-rank bonus tables (report §4) and
    ///     the ElementAttackPower/ElementDefensePower wrapper's rank multiplier (report §6).
    /// </summary>
    public int Title { get; set; }

    /// <summary>
    ///     aHalo — read by StatCalculator TWICE, independently: added directly to all 4 base stats (report §3)
    ///     AND its own separate halo/10 (or +10 at exactly 96) CriticalDefence base bonus (report §5.5).
    /// </summary>
    public int Halo { get; set; }

    /// <summary>aKillOtherTribe (CP) — quest reward type 3 income (report 04 §5); not consumed by StatCalculator.</summary>
    public int ContributionPoints { get; set; }

    /// <summary>
    ///     aTeacherPoint — quest reward type 5 income (report 04 §5, GL_614_QUEST_TEACHER_POINT,
    ///     S04_MyWork02.cpp:7420-7423). A separate counter from the Mentor system's TeacherCharacterId/
    ///     StudentCharacterId bond (those are relationship pointers; this is an accumulated point total) --
    ///     not consumed by StatCalculator.
    /// </summary>
    public int TeacherPoint { get; set; }

    /// <summary>
    ///     Cached output of <see cref="StatCalculator.ComputeEffectiveStats" /> — null until first computed.
    ///     Recompute ONLY on an equipment/buff/level/title/halo CHANGE EVENT (equip, unequip, enchant, level-up,
    ///     buff apply/expire, rebirth...), never once per tick: this mirrors the legacy's own
    ///     SetBasicAbilityFromEquip cache-on-write model (report 11 §1), not the per-tick simulation cost every
    ///     other <see cref="PlayerRuntimeState" /> field pays.
    ///     Refreshed by <see cref="Zone" />'s own tick: seeded from the world-entry snapshot at
    ///     <c>Zone.HandleEnter</c> (computed by <c>EnterWorldHandler</c> from the persisted
    ///     equipment, per <see cref="Fenrir.Application.Game.Inventory.EquipmentService.RecomputeStats" />), and replaced wholesale by
    ///     <c>Zone.ApplyInventoryCommand</c> whenever an accepted move touches
    ///     <see cref="Fenrir.Application.Game.Inventory.ContainerMatrix.Equipment" /> (<c>GenericActionHandler</c>'s own
    ///     precomputed <see cref="Fenrir.Application.Game.Inventory.InventoryZoneCommand.UpdatedStats" />).
    /// </summary>
    public EffectiveStats? Stats { get; set; }

    /// <summary>
    ///     This character's item containers (inventory pages, equipment, store pages) while <c>InWorld</c> --
    ///     see <see cref="Fenrir.Application.Game.Inventory.InventoryState" />'s own remarks for the single-writer contract (identical
    ///     to every other field on this type: mutated ONLY by <see cref="Zone" />'s own tick). Seeded at world
    ///     entry from the A3 world-entry bundle's item result set (<c>Zone.HandleEnter</c>), kept current by
    ///     <c>Zone.ApplyInventoryCommand</c> for every later accepted container move/equip/unequip
    ///     (<c>GenericActionHandler</c>). A plain field-initialized instance (not <c>required</c>): unlike
    ///     the scalar vitals above, an EMPTY inventory is a perfectly valid starting state (a brand-new
    ///     character), so there is no "caller forgot to set this" failure mode to guard against here.
    /// </summary>
    public InventoryState Inventory { get; } = new();

    /// <summary>
    ///     Serializes every economy-affecting request-thread action for THIS character (NPC buy/sell, enchant,
    ///     craft -- <c>GenericActionHandler</c>/<c>EnchantItemHandler</c>/<c>CraftItemHandler</c>) across its
    ///     whole read-<see cref="Inventory" />-snapshot / await-SQL / post-mirror-command sequence. Review
    ///     finding (Phase C/V5 NPC &amp; Economy): each of those handlers reads this character's cached
    ///     <see cref="Inventory" /> on the REQUEST thread, computes a projected container, awaits an
    ///     independent SQL round trip, and only mirrors the result back into <see cref="Inventory" /> on the
    ///     zone's NEXT tick (~50 ms later at 20 Hz) -- nothing stopped a second economy packet for the SAME
    ///     character (the default rate-limiter bucket legally allows a burst of 5) from reading the SAME stale
    ///     pre-mirror snapshot and computing its own projection from it, so both requests' blind SQL
    ///     DELETE+INSERT could each "win," resurrecting an item (or money) the other had already spent -- a
    ///     real duplication vector once V5 started wiring this pattern to money, not just cosmetic staleness.
    ///     Every such handler must acquire this BEFORE reading <see cref="Inventory" /> and release it only
    ///     AFTER the mirror command is posted, so a second in-flight action for this character genuinely waits
    ///     for the first's full round trip (including the zone-tick mirror) rather than merely its SQL call.
    /// </summary>
    public SemaphoreSlim EconomyActionLock { get; } = new(1, 1);

    /// <summary>
    ///     Server-side monotonic counter, independent of the DB's own FlushSequence baseline — incremented once per
    ///     accepted move, never reset, so usp_Character_PersistBatch's idempotence guard (§12.6) always sees a strictly
    ///     increasing value for this character's lifetime in this zone.
    /// </summary>
    public long FlushSequence { get; set; }

    /// <summary>
    ///     AOI grid bookkeeping — which cell this player currently occupies, so <see cref="AoiGrid" /> can detect a
    ///     crossing without a full rescan.
    /// </summary>
    public (int X, int Z) CurrentCell { get; set; }

    /// <summary>
    ///     When the last ACCEPTED move was applied — <see cref="Movement.MovementRules" /> measures elapsed time from
    ///     this, not from the tick's own clock, so a client that skips ticks isn't penalized for the server's schedule.
    /// </summary>
    public DateTime LastMoveUtc { get; set; }

    /// <summary>
    ///     Zone-clock instant (the zone's own simulated <c>TimeSpan</c> clock, not wall time) of the last
    ///     keep-alive rebroadcast of this avatar to its AOI neighbors — the 3.5 s legacy cadence
    ///     (<see cref="Simulation.SimulationClock.AvatarRebroadcastInterval" />) is measured from this. Stamped at
    ///     Enter, refreshed only by the rebroadcast itself (a keep-alive, exactly like the legacy
    ///     <c>tLogicAvatarTick</c> — moving does NOT reset it).
    /// </summary>
    public TimeSpan LastAvatarRebroadcastAt { get; set; }

    /// <summary>
    ///     Stable per-object wire identifier for <c>ZC_AVATAR_ACTION_RECV.UniqueNumber</c>. The wire contract
    ///     names this field but does not resolve its exact semantics (§5.7 lists it as an unelaborated DWORD) —
    ///     CharacterId is reused here since it is already unique and stable for the object's whole lifetime in
    ///     the zone, a reasonable, documented M1 stand-in absent a more specific legacy definition.
    /// </summary>
    public uint UniqueNumber => unchecked((uint)CharacterId);

    /// <summary>
    ///     True from <see cref="Zone.ApplyDeath" /> until the scheduled automatic revive fires (report 12 §4.2:
    ///     ~5 s later, "mCheckDeath"). Not yet consumed by movement/combat gating in this pass — no handler
    ///     currently checks it (the legacy blocks potions/most interactions while <c>aAction.aSort</c> is 11
    ///     stun/12 death); left for the Phase C/V3 combat handler to read, exactly like <see cref="Life" /> and
    ///     the other vitals it will also need.
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    ///     Zone-clock instant (<see cref="LastAvatarRebroadcastAt" />'s own convention: this zone's simulated
    ///     <c>TimeSpan</c> clock, not wall time) at which <see cref="Zone.ApplyDeath" />'s scheduled auto-revive
    ///     fires. Meaningless while <see cref="IsDead" /> is false. The revive is always IN PLACE (report 12
    ///     §4.2/§4.3: the legacy only auto-clears the death flag locally after the delay, never teleports) --
    ///     no destination zone/position needs to be carried alongside this timestamp.
    /// </summary>
    public TimeSpan ReviveAtZoneClock { get; set; }

    /// <summary>
    ///     Mirrors the legacy's own persistent <c>mDATA.aAction.aSort</c> (report 05 §7/§12 §4.2) -- the last
    ///     accepted avatar action's Sort, updated by <see cref="Zone.HandleMove" /> alongside position for
    ///     EVERY action, not just movement (the wire carries one unified action for move/sit/skill-cast alike).
    ///     Read by <see cref="Simulation.MeditationRegenSystem" /> (31 = sitting/meditating) and by
    ///     <c>Zone.ApplySkillCast</c>'s own gating. 0 = idle, matching the legacy default.
    /// </summary>
    public int ActionSort { get; set; }

    /// <summary>
    ///     The skill number/grade points riding on the last accepted action (<c>ActionInfo.SkillNumber</c>/
    ///     <c>SkillGradeNum1/2</c>) -- kept live (not reset to 0 between ticks, matching the legacy's own
    ///     persistent <c>mDATA.aAction</c>) so <see cref="Simulation.MeditationRegenSystem" /> can resolve
    ///     "which sit-skill is this player using" every legacy tick without the client having to resend it.
    /// </summary>
    public int ActionSkillNumber { get; set; }

    public int ActionSkillGradeNum1 { get; set; }
    public int ActionSkillGradeNum2 { get; set; }

    /// <summary>
    ///     Live BUFF_INFO mirror (35 slots x [value, duration-in-legacy-ticks], report 12 §4.2 / report §7
    ///     point 4) -- fed to <see cref="Stats.StatCalculator.ComputeEffectiveStats" /> as the buff snapshot and
    ///     decremented/expired by <see cref="Simulation.BuffExpirySystem" /> every legacy tick.
    /// </summary>
    /// <remarks>
    ///     Deliberately a FRESH per-instance array (never <c>Fenrir.Contracts.Packets.Shared.WorldStateTemplates.ZeroedBuffInfo</c>,
    ///     which is one process-wide SHARED static instance) -- reusing that template here would let every
    ///     player's buffs alias the same backing <c>int[]</c> and corrupt each other.
    /// </remarks>
    public BuffInfo Buffs { get; } = new() { Buff = new int[70] };

    /// <summary>
    ///     Zone-clock instant this character last entered/re-entered a zone (fresh world entry OR an in-process
    ///     handoff arrival) -- the legacy's <c>mTickCountFor01SecondForProtect</c> (report 05 §4 point 1:
    ///     <c>PROTECT_TICK</c> = 20 legacy ticks / 10s anti-chain-attack grace window, checked for BOTH sides of
    ///     an attack). <c>Server/ts25zone/S04_MyWork02.cpp:838,1783</c> are the field's ONLY two write sites in
    ///     the whole legacy source (registration, and the client's one-time post-load "aSort==0" action) --
    ///     it is NEVER refreshed by taking or dealing damage, so this is a one-shot spawn/arrival grace period,
    ///     not a rolling "stop hitting me" cooldown (a prior pass here refreshed it on every hit taken, which
    ///     made two players who traded even a single blow mutually unable to fight anyone for the next 10s --
    ///     see <see cref="Zone.HandleEnter" />, the sole write site now). NULL (not <see cref="TimeSpan.Zero" />)
    ///     means "never entered a zone yet" -- zero is a real, reachable zone-clock instant (a fresh zone / a
    ///     player who entered before any tick elapsed), so using it as the "never" sentinel would incorrectly
    ///     gate every player's first attack during the zone's first
    ///     <see cref="Combat.CombatResolver.ProtectDuration" /> of simulated lifetime.
    /// </summary>
    public TimeSpan? ZoneEntryAtZoneClock { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last accepted skill cast (Sort=30) -- a global, one-cast-per-
    ///     legacy-tick anti-flood gate modeled after the VERIFIED USE_INVENTORY_ITEM anti-flood pattern (report
    ///     04 §2: "1 seul 'use' par tick logique par joueur"), since reports 04/05/12 do not document a distinct
    ///     per-skill reuse-delay for generic (non-attack) skill casts -- see <c>Zone.ApplySkillCast</c>'s own
    ///     remarks and this task's StructuredOutput open issues. NULL means "never cast" -- same zero-is-a-real-
    ///     instant reasoning as <see cref="LastDamagedAtZoneClock" />.
    /// </summary>
    public TimeSpan? LastSkillCastAtZoneClock { get; set; }

    /// <summary>
    ///     Loaded ONCE at world entry from <c>admin.usp_Mute_GetActiveForCharacter</c> (report 06 §1.7) --
    ///     a hidden flag, never re-queried per chat message (the legacy's own synchronous
    ///     <c>U_ZONE_CHECK_MUTE_FOR_PLAYUSER_SEND</c> call is replaced by this one-shot cache, exactly
    ///     like the report's own note prescribes). A mute lifted or newly applied mid-session is only
    ///     picked up on the player's NEXT world entry -- a documented, deliberate simplification (Social/
    ///     ChatRouter's own remarks), not a live subscription.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    ///     This character's guild, if any -- loaded once at world entry (<c>game.usp_GuildMember_GetByCharacter</c>),
    ///     same one-shot-cache posture as <see cref="IsMuted" />. Null means "no guild". Used for guild
    ///     chat/announcement routing (Social/Chat/ChatRouter) and the AVATAR_INFO GuildName/GuildRole
    ///     fields -- <see cref="GuildRoleDb" /> is the DB-side enum (0 member/1 sub-master/2 master); see
    ///     <see cref="Social.GuildRoleCodec" /> for the translation to the legacy wire's inverted encoding.
    /// </summary>
    public int? GuildId { get; set; }

    public string GuildName { get; set; } = "";

    public byte GuildRoleDb { get; set; }

    /// <summary>
    ///     This character's cosmetic in-guild title (legacy gMemberCall, game.GuildMembers.CallName --
    ///     Phase C/V7 Guilds &amp; Tribes) -- loaded once at world entry alongside <see cref="GuildId" />,
    ///     mutated live by <c>GuildActionHandler</c> tSort 10 (self) or mirrored onto a DIFFERENT member's
    ///     zone via <c>Zone.ApplyGuildMembershipCommand</c> (kick/AGM-demote clear it, title sets it) --
    ///     same posture as every other guild membership field on this type.
    /// </summary>
    public string GuildCallName { get; set; } = "";

    /// <summary>
    ///     This character's tribe role -- loaded once at world entry (<c>game.usp_TribeRole_GetForCharacter</c>),
    ///     matching <c>ReturnTribeRole</c>'s own encoding directly (0 = regular, 1 = master, 2 = sub-master;
    ///     Server/Header/function.h:92-114). Used to gate CZ_TRIBE_ANNOUNCEMENT_SEND and to populate
    ///     ZC_TRIBE_NOTICE_RECV's TribeRole field.
    /// </summary>
    public byte TribeRole { get; set; }

    /// <summary>
    ///     aUseOrnament (TRIBE_WORK tSort 9/10, doc 10 §2 -- MyFactor's <c>GetUsedOrnament</c>, report 11
    ///     §2/§5.1-§5.4). OPEN ISSUE: session-scoped only, NOT loaded from/flushed to game.Characters --
    ///     no batch has added a persisted column or a write-behind path for it yet (the actual HP/MP/DMG/DEF
    ///     ornament BONUS also depends on aGoldTime/aSilverTime, an entirely separate, unimplemented
    ///     "ornament rental" subsystem StatCalculator does not model either), so wiring persistence for
    ///     this ONE flag in isolation would be inert -- deferred alongside that subsystem, see this task's
    ///     StructuredOutput.
    /// </summary>
    public bool UseOrnament { get; set; }

    /// <summary>
    ///     aProtectForHalo (TRIBE_WORK tSort 7 halo enchant, doc 10 §2 -- a consumable charge that absorbs
    ///     one "halo -1" downgrade instead of letting it happen). Same OPEN ISSUE as <see cref="UseOrnament" />:
    ///     session-scoped only, never loaded from/flushed to game.Characters (no batch has modeled how a
    ///     player ACQUIRES this charge either -- an item-use effect outside this batch's perimeter).
    /// </summary>
    public int ProtectForHalo { get; set; }

    /// <summary>
    ///     aBonusItemLevel (TRIBE_WORK tSort 8, doc 10 §2 -- which level-up milestone's bonus-item claim is
    ///     pending). Same OPEN ISSUE as <see cref="UseOrnament" />: session-scoped only, always 0 in this
    ///     batch since no leveling-milestone system grants it yet (a Combat/leveling concern, out of this
    ///     V7 Guilds &amp; Tribes perimeter) -- the CONSUMING dispatch (tSort 8) is fully implemented and
    ///     correctly rejects a zero value, matching the legacy's own <c>Quit()</c> for that case exactly.
    /// </summary>
    public int BonusItemLevel { get; set; }

    /// <summary>aBonusItemValue -- companion flag to <see cref="BonusItemLevel" />, same OPEN ISSUE.</summary>
    public bool BonusItemValue { get; set; }

    /// <summary>
    ///     aPreviousTribe (TRIBE_WORK tSort 8's LV_M33 tier item selection, doc 10 §2). Same OPEN ISSUE as
    ///     <see cref="UseOrnament" />: no rebirth/tribe-transition system exists yet to populate this from
    ///     anything other than the character's current <see cref="Tribe" /> -- defaults to <see cref="Tribe" />
    ///     at world entry (Zone.HandleEnter), a documented, reasonable "never transferred tribes" inference,
    ///     not independently verified against a real legacy default.
    /// </summary>
    public byte PreviousTribe { get; set; }

    /// <summary>
    ///     This character's own friend list (game.CharacterFriends, slot -&gt; friend CharacterId), loaded
    ///     once at world entry and mutated directly by <c>FriendAddHandler</c>/<c>FriendRemoveHandler</c> --
    ///     a deliberate "own-character, request-thread-mutated" exception to the single-writer invariant
    ///     (friends are not a D7 value-object economy concern, so the stricter zone-tick-only posture
    ///     <see cref="Inventory" /> carries does not apply here). Review finding (Phase C/V6): a zone-transfer
    ///     handoff (<c>ZoneTransfer.CreateEnterData</c>/<c>Zone.HandleEnter</c>) carries this SAME dictionary
    ///     instance across to the target zone's Enter and enumerates it there on the TARGET zone's own tick
    ///     thread -- a request-thread Add/Remove racing that enumeration (in flight right as the character
    ///     transfers) could throw <c>InvalidOperationException</c> on the tick thread, an earlier comment here
    ///     incorrectly asserted no such reader existed. <see cref="ConcurrentDictionary{TKey,TValue}" /> makes
    ///     that specific race safe (a concurrent enumerator never throws, though it may miss or double-see an
    ///     in-flight mutation) without adopting the full zone-tick-only posture the rest of this type uses.
    /// </summary>
    public ConcurrentDictionary<byte, int> Friends { get; } = new();

    /// <summary>
    ///     This character's teacher (master), if any -- game.Characters.TeacherCharacterId, loaded once at
    ///     world entry and mutated live by <c>MentorStartHandler</c>/<c>MentorEndHandler</c> (same
    ///     request-thread-mutated precedent as <see cref="Friends" />). Null = no teacher.
    /// </summary>
    public int? TeacherCharacterId { get; set; }

    /// <summary>This character's student, if any (only meaningful for a master) -- game.Characters.StudentCharacterId, same posture as <see cref="TeacherCharacterId" />.</summary>
    public int? StudentCharacterId { get; set; }

    // ---- Server Logic V9 Progression ----

    /// <summary>
    ///     The linear per-tribe quest chain's permanent progression index (legacy <c>aQuestInfo[0]</c>,
    ///     report 04 §5) -- survives completion/abandon (only the 4 fields below reset to 0 on either).
    ///     game.CharacterQuests.StepPermanent.
    /// </summary>
    public int QuestStepPermanent { get; set; }

    /// <summary>Legacy <c>aQuestInfo[1]</c> -- 0/1 "a quest is currently active" flag (NOT a quest id despite the DB column's legacy-derived name, game.CharacterQuests.ActiveQuestId).</summary>
    public int QuestActiveFlag { get; set; }

    /// <summary>Legacy <c>aQuestInfo[2]</c> -- the active quest's <c>qSort</c> (1-8, see <see cref="Quests.QuestStateMachine" />'s remarks for the verified 8, not 6, real types). 0 = no active quest.</summary>
    public int QuestSort { get; set; }

    /// <summary>Legacy <c>aQuestInfo[3]</c> -- target item id / exchange phase, meaning depends on <see cref="QuestSort" />.</summary>
    public int QuestTargetPhase { get; set; }

    /// <summary>Legacy <c>aQuestInfo[4]</c> -- kill counter / second exchange item, meaning depends on <see cref="QuestSort" />. Incremented by the monster-kill hook (qSort 1/5) -- see <see cref="Monsters.MonsterSpawnScheduler" />'s ProcessDeath.</summary>
    public int QuestKillCounter { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aJoinWar</c> (MISSION_DATE, report 04 CZ_MISSION_COMPLETE_SEND) -- gates
    ///     the daily-mission reward claim (&gt;= MAX_MISSION_JOIN_WAR_NUM=1). OPEN ISSUE: its only verified
    ///     increment hook (S07_MyGame01.cpp, regular-war "zone049" participation) lives entirely inside the
    ///     special war-event state machines report 05 §13 already documents as out of Fenrir's scope, so
    ///     this stays 0 for every character until that subsystem exists -- a real, correctly-gated, but
    ///     currently unreachable mechanic, not a stub.
    /// </summary>
    public int MissionJoinWar { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aKillOtherTribe</c> -- a SEPARATE counter from <see cref="ContributionPoints" />
    ///     (aKillOtherTribe/CP), gates the daily-mission claim (&gt;= 10). Same "real but currently
    ///     unreachable" posture as <see cref="MissionJoinWar" />: its increment hook lives inside
    ///     <c>ProcessForKillOtherTribe</c> (PvP-kill CP/XP pipeline), verified NOT implemented (see
    ///     <see cref="World.Zone.ApplyDeath" />'s own remarks on <c>DeathCause.PlayerKill</c>).
    /// </summary>
    public int MissionKillOtherTribe { get; set; }

    /// <summary>Legacy <c>aMissionDate.aKillMonster</c> -- tracked (echoed on ZC 163) but its own claim-gate is compiled OUT in EU33 (USE_DAILY_UI_MONSTER_KILL is OFF, verified) so it never blocks a claim.</summary>
    public int MissionKillMonster { get; set; }

    /// <summary>Legacy <c>aMissionDate.aPlayTime</c> -- same "tracked, gate compiled out" posture as <see cref="MissionKillMonster" />.</summary>
    public int MissionPlayTime { get; set; }

    /// <summary>Legacy <c>aAutoState</c> (0/1) -- CZ_AUTO_CONFIG_SEND/ZC_AUTO_CONFIG_RECV (opcode 99/123).</summary>
    public bool AutoHuntEnabled { get; set; }

    /// <summary>
    ///     The raw 112-byte AUTO_HUNT blob (<see cref="AutoHunt" />), <c>CopyMemory</c>'d verbatim from the
    ///     client with NO server-side content validation (verified S04_MyWork02.cpp:13508-13614) -- an
    ///     anti-cheat surface this pass deliberately does not close, matching the legacy exactly. Null =
    ///     never configured (a fresh character). The autonomous bot LOOP itself (auto-attack/auto-loot/
    ///     auto-potion consumption once enabled) is explicitly OUT OF SCOPE for this pass -- see this
    ///     feature's StructuredOutput open issues; only the config-storage/gating half is implemented.
    /// </summary>
    public AutoHunt? AutoHuntConfig { get; set; }

    /// <summary>Legacy <c>aAutoLifeRatio</c> (0-5) -- CZ_CHANGE_AUTO_INFO, silently stored, never echoed back.</summary>
    public byte AutoLifeRatio { get; set; }

    /// <summary>Legacy <c>aAutoManaRatio</c> (0-5) -- same posture as <see cref="AutoLifeRatio" />.</summary>
    public byte AutoManaRatio { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last CZ_HERORANK_INFO_SEND reply for the PREVIOUS period
    ///     (ZC 148) -- the verified 2.5s per-user throttle (<c>mTickForRankingPre</c>, report 12/contracts
    ///     07). Null = never queried yet (always due).
    /// </summary>
    public TimeSpan? LastHeroRankingPreviousQueryAtZoneClock { get; set; }

    /// <summary>Same throttle posture as <see cref="LastHeroRankingPreviousQueryAtZoneClock" />, for the CURRENT period (ZC 150, <c>mTickForRankingCur</c>).</summary>
    public TimeSpan? LastHeroRankingCurrentQueryAtZoneClock { get; set; }

    /// <summary>
    ///     A Fenrir-side SIMPLIFICATION of the legacy's per-pet-ITEM growth counter (report 12 §2.1,
    ///     <c>wAvatar.aInventory[EPET-slot][4]</c>, up to 640,000,000) -- tracked per CHARACTER instead of
    ///     per item instance (see game.Characters.PetGrowth's own migration header for why). Reset to the
    ///     newly-equipped pet's base tier whenever the Equipment container's EPET slot (<see cref="Pets.PetSlots" />)
    ///     changes to a DIFFERENT item id. Read by <see cref="Pets.PetGrowthCalculator" /> to populate
    ///     <see cref="Stats.PetStatContribution" />.
    /// </summary>
    public int PetGrowth { get; set; }

    /// <summary>
    ///     0-100 activity (report 12 §2.1) -- decays -1 every 60 legacy ticks (30 s,
    ///     <see cref="Simulation.PetActivitySystem" />) while a pet is equipped and not already at 0; gates
    ///     the ATTACK contribution only (<see cref="Pets.PetGrowthCalculator" />'s own remarks: verified
    ///     Life/Mana/Defense contributions do NOT gate on activity, only Attack does -- GameSystem_07_Pet.cpp
    ///     read in full for this pass).
    /// </summary>
    public byte PetActivity { get; set; }

    /// <summary>The ItemId last seen equipped in the pet slot -- lets <see cref="World.Zone" /> detect a pet SWAP (not just any equipment change) to reset <see cref="PetGrowth" />/<see cref="PetActivity" />. 0 = no pet equipped.</summary>
    public int LastSeenPetItemId { get; set; }

    /// <summary>Legacy-tick accumulator for <see cref="Simulation.PetActivitySystem" />'s own 60-tick (30 s) decay cadence -- never read by anything else.</summary>
    public int PetActivityDecayTicks { get; set; }

    // ---- Server Logic V8 Player Commerce & Cash ----

    /// <summary>
    ///     Legacy <c>aPShopState == 1</c> (contracts/04_commerce.md CZ_START/END_PSHOP_SEND) -- true while
    ///     this character has a LIVE personal-shop stall open. Deliberately NOT set for a proxy/offline
    ///     shop (the legacy itself never touches aPShopState for the proxy path either --
    ///     <c>S04_MyWork02.cpp:6332</c>, verified: <c>aPShopState = isProxyShop ? 0 : 1</c> -- an offline
    ///     shop's whole state lives in SQL, game.OfflineShops, since it must keep working while this
    ///     character is offline). Mutated directly by THIS character's own request-thread handlers
    ///     (<c>OpenShopStallHandler</c>/<c>CloseShopStallHandler</c>) -- same accepted "own-character,
    ///     request-thread-mutated" exception as <see cref="Friends" />/<see cref="TeacherCharacterId" />:
    ///     no D7 value-object concern here, the advertised items never actually leave <see cref="Inventory" />
    ///     while merely "for sale" (the real source of truth for "is this item still available" a purchase
    ///     re-validates against is always the live <see cref="Inventory" /> slot, never this cached
    ///     display copy -- verified <c>S04_MyWork02.cpp:6998-7009</c>). A SELLER's copy of this pair is only
    ///     ever mutated by a DIFFERENT character's (the buyer's) request thread through
    ///     <see cref="Social.Pshop.PshopZoneCommand" />, routed onto the seller's OWN zone tick -- the exact
    ///     same cross-character-write fix <see cref="Social.Mentor.MentorZoneCommand" /> already established
    ///     for <see cref="TeacherCharacterId" />.
    /// </summary>
    public bool PshopOpen { get; set; }

    /// <summary>The currently-advertised stall listing while <see cref="PshopOpen" /> is true; stale/meaningless otherwise (not cleared on close, matching the legacy's own "don't bother zeroing it" posture -- callers must always gate on <see cref="PshopOpen" /> first).</summary>
    public PshopInfo? PshopListing { get; set; }
}
