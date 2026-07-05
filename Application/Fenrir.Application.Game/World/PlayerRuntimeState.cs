using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.Stats;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;

namespace Fenrir.Application.Game.World;

/// <summary>
///     A player's in-memory, authoritative state while <c>InWorld</c>. Mutated only by <see cref="Zone.RunAsync" />
///     -- every other thread posts a <see cref="ZoneCommand" /> and waits for the next tick instead.
/// </summary>
public sealed class PlayerRuntimeState
{
    private static readonly ImmutableArray<(int ItemId, int Count)> DefaultBottleSlots =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

    private static readonly ImmutableArray<(int SkillId, int Grade)> DefaultAutoBuffSkill =
    [
        (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)
    ];

    public required int CharacterId { get; init; }
    public required IPacketSession Session { get; init; }
    public required string Name { get; init; }
    public required byte Tribe { get; init; }
    public required byte Gender { get; init; }
    public required byte HeadType { get; init; }
    public required byte FaceType { get; init; }

    /// <summary>
    ///     aLevel1. Mutated only by <see cref="Zone.GrantMonsterKillExperience" /> on a level-up -- every other
    ///     read site treats this as the character's current level.
    /// </summary>
    public required short Level { get; set; }

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
    ///     Spent base stat points (legacy aVit/aStr/aInt/aDex). Note: StatInt feeds the Ki stat and StatDex
    ///     feeds Wisdom in <see cref="StatCalculator" />, not Intelligence/Dexterity literally.
    /// </summary>
    public int StatVit { get; set; }

    public int StatStr { get; set; }
    public int StatInt { get; set; }
    public int StatDex { get; set; }

    /// <summary>Unspent stat/skill points (aStatPoint/aSkillPoint) — spend-on-levelup UI reads these, not StatCalculator.</summary>
    public int StatPoints { get; set; }

    public int SkillPoints { get; set; }

    /// <summary>
    ///     Learned-skill slots (legacy <c>aSkill[40][0..1]</c>) -- an absent slot is "empty," same convention
    ///     as <see cref="Inventory" />. Mutated only by <see cref="Zone" />'s own tick.
    /// </summary>
    public ImmutableDictionary<byte, LearnedSkill> LearnedSkills { get; set; } =
        ImmutableDictionary<byte, LearnedSkill>.Empty;

    /// <summary>Total XP (aExp1/aExp2 combined).</summary>
    public long Experience { get; set; }

    /// <summary>
    ///     aLevel2, the post-cap "high level" rebirth ladder (1-12, MAX_LIMIT_HIGH_LEVEL_NUM) -- only reachable
    ///     once <see cref="Level" /> reaches <see cref="Stats.LevelProgressionCalculator.MaxLevel" />. No
    ///     Fenrir-side kill-experience path grants this yet (see <see cref="Stats.LevelProgressionCalculator" />'s
    ///     own remarks); <c>TribeActionHandler.HandleRebirthAsync</c>'s Max Rebirth gate is its only consumer today.
    /// </summary>
    public short Level2 { get; set; }

    /// <summary>aExp2 -- Level2's own XP counter, reset to 0 on every successful Max Rebirth.</summary>
    public int Exp2 { get; set; }

    /// <summary>aRebirthNum -- real cap is 6 (app-enforced by TribeActionHandler, not this field). Read by StatCalculator's CriticalDefence and Critical wrapper bonuses.</summary>
    public int RebirthCount { get; set; }

    /// <summary>aTitle (category*100 + rank 1-14) -- read by StatCalculator's title-rank bonus tables.</summary>
    public int Title { get; set; }

    /// <summary>
    ///     aHalo -- read twice independently by StatCalculator: added directly to all 4 base stats, and again for its own
    ///     CriticalDefence bonus.
    /// </summary>
    public int Halo { get; set; }

    /// <summary>aKillOtherTribe (CP) -- quest reward type 3 income; not consumed by StatCalculator.</summary>
    public int ContributionPoints { get; set; }

    /// <summary>
    ///     aTeacherPoint -- quest reward type 5 income. A separate counter from the Mentor system's
    ///     TeacherCharacterId/StudentCharacterId bond.
    /// </summary>
    public int TeacherPoint { get; set; }

    /// <summary>
    ///     Cached output of <see cref="StatCalculator.ComputeEffectiveStats" /> -- null until first computed.
    ///     Recompute only on an equipment/buff/level/title/halo change event, never once per tick.
    /// </summary>
    public EffectiveStats? Stats { get; set; }

    /// <summary>
    ///     This character's item containers (inventory pages, equipment, store pages) while <c>InWorld</c> --
    ///     mutated only by <see cref="Zone" />'s own tick, same single-writer contract as every other field.
    /// </summary>
    public InventoryState Inventory { get; } = new();

    /// <summary>
    ///     Serializes every economy-affecting request-thread action for this character (NPC buy/sell, enchant,
    ///     craft) across its read-<see cref="Inventory" />-snapshot / await-SQL / post-mirror-command
    ///     sequence. Without this, two concurrent requests for the same character could both read the same
    ///     stale pre-mirror snapshot and duplicate an item or money. Acquire before reading
    ///     <see cref="Inventory" />, release only after the mirror command is posted.
    /// </summary>
    public SemaphoreSlim EconomyActionLock { get; } = new(1, 1);

    /// <summary>
    ///     Server-side monotonic counter, independent of the DB's own FlushSequence baseline -- incremented
    ///     once per accepted move, never reset, so <c>usp_Character_PersistBatch</c>'s idempotence guard
    ///     always sees a strictly increasing value for this character's lifetime in this zone.
    /// </summary>
    public long FlushSequence { get; set; }

    /// <summary>
    ///     AOI grid bookkeeping -- which cell this player currently occupies, so <see cref="AoiGrid" /> can detect a
    ///     crossing without a full rescan.
    /// </summary>
    public (int X, int Z) CurrentCell { get; set; }

    /// <summary>
    ///     When the last accepted move was applied -- <see cref="Movement.MovementRules" /> measures elapsed
    ///     time from this, not from the tick's own clock, so a client that skips ticks isn't penalized.
    /// </summary>
    public DateTime LastMoveUtc { get; set; }

    /// <summary>
    ///     Zone-clock instant of the last keep-alive rebroadcast of this avatar to its AOI neighbors -- the
    ///     3.5 s legacy cadence is measured from this. Stamped at Enter, refreshed only by the rebroadcast
    ///     itself (moving does NOT reset it).
    /// </summary>
    public TimeSpan LastAvatarRebroadcastAt { get; set; }

    /// <summary>
    ///     Stable per-object wire identifier for <c>ZC_AVATAR_ACTION_RECV.UniqueNumber</c>. CharacterId is
    ///     reused here since it is already unique and stable for the object's whole lifetime in the zone.
    /// </summary>
    public uint UniqueNumber => unchecked((uint)CharacterId);

    /// <summary>True from <see cref="Zone.ApplyDeath" /> until the scheduled automatic revive fires.</summary>
    public bool IsDead { get; set; }

    /// <summary>
    ///     Zone-clock instant at which <see cref="Zone.ApplyDeath" />'s scheduled auto-revive fires.
    ///     Meaningless while <see cref="IsDead" /> is false. The revive is always in place -- no destination
    ///     zone/position needs to be carried alongside this timestamp.
    /// </summary>
    public TimeSpan ReviveAtZoneClock { get; set; }

    /// <summary>
    ///     Mirrors the legacy's own persistent <c>mDATA.aAction.aSort</c> -- the last accepted avatar action's
    ///     Sort, updated by <see cref="Zone.HandleMove" /> alongside position for every action, not just
    ///     movement. Read by <see cref="Simulation.MeditationRegenSystem" /> (31 = sitting/meditating). 0 = idle.
    /// </summary>
    public int ActionSort { get; set; }

    /// <summary>
    ///     The skill number/grade points riding on the last accepted action -- kept live (not reset between
    ///     ticks) so <see cref="Simulation.MeditationRegenSystem" /> can resolve which sit-skill is in use
    ///     every legacy tick without the client resending it.
    /// </summary>
    public int ActionSkillNumber { get; set; }

    public int ActionSkillGradeNum1 { get; set; }
    public int ActionSkillGradeNum2 { get; set; }

    /// <summary>
    ///     Live BUFF_INFO mirror (35 slots x [value, duration-in-legacy-ticks]) -- fed to
    ///     <see cref="Stats.StatCalculator.ComputeEffectiveStats" /> and decremented/expired by
    ///     <see cref="Simulation.BuffExpirySystem" /> every legacy tick.
    /// </summary>
    /// <remarks>
    ///     Deliberately a fresh per-instance array (never
    ///     <c>Fenrir.Contracts.Packets.Shared.WorldStateTemplates.ZeroedBuffInfo</c>,
    ///     a shared static instance) -- reusing that template would let every player's buffs alias the same backing
    ///     <c>int[]</c>.
    /// </remarks>
    public BuffInfo Buffs { get; } = new() { Buff = new int[70] };

    /// <summary>
    ///     Zone-clock instant this character last entered/re-entered a zone -- the legacy's anti-chain-attack
    ///     grace window (~10s, checked on both sides of an attack). Never refreshed by taking/dealing damage:
    ///     it is a one-shot spawn/arrival grace period, not a rolling cooldown (an earlier version here
    ///     refreshed it on every hit, which made two players who traded blows mutually unable to fight anyone
    ///     for the next 10s). Null, not <see cref="TimeSpan.Zero" />, means "never entered" -- zero is itself
    ///     a reachable zone-clock instant.
    /// </summary>
    public TimeSpan? ZoneEntryAtZoneClock { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last accepted skill cast (Sort=30) -- a global,
    ///     one-cast-per-legacy-tick anti-flood gate. Null means "never cast."
    /// </summary>
    public TimeSpan? LastSkillCastAtZoneClock { get; set; }

    /// <summary>
    ///     Wall-clock instant of this character's last accepted CZ_HEARTBEAT_SEND (op151) -- deliberately
    ///     wall-clock, not zone-clock, since <see cref="Handlers.ZoneReadyHandler" />/<see cref="Handlers.HeartbeatHandler" />
    ///     run on the session loop and cannot see Zone's private simulated clock (same posture as
    ///     <see cref="FishingCastAtUtc" />). Null means "no heartbeat seen yet," matching legacy's
    ///     <c>mLastSentHeartbeat != -1</c> guard -- a session's very first ZoneReady always finds this null.
    /// </summary>
    public DateTime? LastSentHeartbeat { get; set; }

    /// <summary>
    ///     Legacy <c>mPrevSent</c> -- the <c>LastSend</c> counter value from this character's last accepted
    ///     heartbeat, compared by <see cref="Handlers.HeartbeatHandler" /> against the next one to reject a
    ///     replayed frame. Null (not 0) means "never sent" -- unlike legacy's truthy-zero sentinel, a
    ///     legitimate first <c>LastSend</c> of 0 is not mistaken for "no heartbeat yet."
    /// </summary>
    public uint? PrevSentHeartbeat { get; set; }

    /// <summary>
    ///     Wall-clock instant <see cref="Handlers.ZoneReadyHandler" /> last completed this character's op13
    ///     handshake without disconnecting it. Legacy restamps <c>mConnectTime</c> on every
    ///     CLIENT_OK_FOR_ZONE_SEND; Fenrir's op13 is a one-shot Registering-to-InWorld handshake (ZoneReadyRequest's
    ///     AllowedStates), so this is set exactly once per session. Null until then.
    /// </summary>
    public DateTime? ConnectTime { get; set; }

    /// <summary>
    ///     Legacy <c>mAutoTimeHack</c> -- strikes against a client that declares itself auto-hunting
    ///     (ZoneReadyRequest.AutoState &gt; 0) while <see cref="AutoHuntEnabled" /> is false server-side.
    ///     <see cref="Handlers.ZoneReadyHandler" /> disconnects on the 3rd strike, matching legacy exactly.
    /// </summary>
    public int AutoTimeHack { get; set; }

    /// <summary>
    ///     Loaded once at world entry -- a hidden flag, never re-queried per chat message. A mute lifted or
    ///     newly applied mid-session is only picked up on the player's next world entry.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    ///     This character's guild, if any -- loaded once at world entry, same one-shot-cache posture as
    ///     <see cref="IsMuted" />. Null means "no guild". <see cref="GuildRoleDb" /> is the DB-side enum
    ///     (0 member/1 sub-master/2 master); see <see cref="Social.GuildRoleCodec" /> for the legacy wire's
    ///     inverted encoding.
    /// </summary>
    public int? GuildId { get; set; }

    public string GuildName { get; set; } = "";

    public byte GuildRoleDb { get; set; }

    /// <summary>
    ///     Cosmetic in-guild title (legacy gMemberCall, game.GuildMembers.CallName) -- loaded once at world entry
    ///     alongside <see cref="GuildId" />.
    /// </summary>
    public string GuildCallName { get; set; } = "";

    /// <summary>
    ///     This character's tribe role -- loaded once at world entry, matching <c>ReturnTribeRole</c>'s own
    ///     encoding directly (0 = regular, 1 = master, 2 = sub-master).
    /// </summary>
    public byte TribeRole { get; set; }

    /// <summary>
    ///     aUseOrnament. Session-scoped only -- not yet loaded from/flushed to game.Characters (no persisted column
    ///     exists yet).
    /// </summary>
    public bool UseOrnament { get; set; }

    /// <summary>
    ///     aProtectForHalo -- a consumable charge that absorbs one "halo -1" downgrade. Same open issue as
    ///     <see cref="UseOrnament" />: session-scoped only.
    /// </summary>
    public int ProtectForHalo { get; set; }

    /// <summary>
    ///     aBonusItemLevel -- which level-up milestone's bonus-item claim is pending. Session-scoped only; always 0 until
    ///     a leveling-milestone system grants it.
    /// </summary>
    public int BonusItemLevel { get; set; }

    /// <summary>aBonusItemValue -- companion flag to <see cref="BonusItemLevel" />, same open issue.</summary>
    public bool BonusItemValue { get; set; }

    /// <summary>
    ///     aTribeNotifyNum -- remaining CZ_TRIBE_NOTIFY_SEND (opcode 112) "announcement scroll" charges.
    ///     Session-scoped only; always 0 until the UseInventoryItem scroll-item family (op 23) is implemented,
    ///     same open issue as <see cref="BonusItemLevel" />.
    /// </summary>
    public int TribeNotifyScrollCount { get; set; }

    /// <summary>
    ///     aPreviousTribe. No rebirth/tribe-transition system exists yet to populate this from anything other
    ///     than the character's current <see cref="Tribe" /> -- defaults to <see cref="Tribe" /> at world entry.
    /// </summary>
    public byte PreviousTribe { get; set; }

    /// <summary>
    ///     aBottle/aBottleCount (10 slots, ItemId 0 = empty). Session-scoped only -- same open issue as
    ///     <see cref="UseOrnament" />, no persisted column exists yet. Populated by UseInventoryItemHandler's
    ///     iSort==26 family, consumed/decremented by DrinkBottleHandler.
    /// </summary>
    public ImmutableArray<(int ItemId, int Count)> BottleSlots { get; set; } = DefaultBottleSlots;

    /// <summary>
    ///     This character's own friend list (slot -&gt; friend CharacterId), mutated directly by friend-add/
    ///     remove handlers on the request thread -- a deliberate exception to the single-writer invariant. A
    ///     zone-transfer handoff carries this same dictionary instance to the target zone and enumerates it on
    ///     that zone's own tick thread, so a concurrent Add/Remove must not throw during enumeration --
    ///     <see cref="ConcurrentDictionary{TKey,TValue}" /> makes that race safe.
    /// </summary>
    public ConcurrentDictionary<byte, int> Friends { get; } = new();

    /// <summary>
    ///     This character's teacher (master), if any -- mutated live by mentor start/end handlers (same request-thread
    ///     exception as <see cref="Friends" />). Null = no teacher.
    /// </summary>
    public int? TeacherCharacterId { get; set; }

    /// <summary>
    ///     This character's student, if any (only meaningful for a master) -- same posture as
    ///     <see cref="TeacherCharacterId" />.
    /// </summary>
    public int? StudentCharacterId { get; set; }

    /// <summary>
    ///     The linear per-tribe quest chain's permanent progression index (legacy <c>aQuestInfo[0]</c>) -- survives
    ///     completion/abandon.
    /// </summary>
    public int QuestStepPermanent { get; set; }

    /// <summary>
    ///     Legacy <c>aQuestInfo[1]</c> -- a 0/1 "quest active" flag, NOT a quest id despite the DB column's
    ///     legacy-derived name.
    /// </summary>
    public int QuestActiveFlag { get; set; }

    /// <summary>Legacy <c>aQuestInfo[2]</c> -- the active quest's <c>qSort</c> (1-8). 0 = no active quest.</summary>
    public int QuestSort { get; set; }

    /// <summary>Legacy <c>aQuestInfo[3]</c> -- target item id / exchange phase, meaning depends on <see cref="QuestSort" />.</summary>
    public int QuestTargetPhase { get; set; }

    /// <summary>
    ///     Legacy <c>aQuestInfo[4]</c> -- kill counter / second exchange item, meaning depends on
    ///     <see cref="QuestSort" />. Incremented by the monster-kill hook (qSort 1/5).
    /// </summary>
    public int QuestKillCounter { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aJoinWar</c> -- gates the daily-mission reward claim (&gt;= 1). Its only
    ///     verified increment hook lives inside the war-event state machines (out of scope here), so this
    ///     stays 0 for every character until that subsystem exists -- a real, correctly-gated, but currently
    ///     unreachable mechanic, not a stub.
    /// </summary>
    public int MissionJoinWar { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aKillOtherTribe</c> -- a separate counter from <see cref="ContributionPoints" />,
    ///     gates the daily-mission claim (&gt;= 10). Incremented by <c>Zone.ApplyPvpKillMissionProgress</c>, gated
    ///     by <see cref="Fenrir.Application.Game.Combat.KillCooldownTracker" /> so repeat-farming one victim only
    ///     counts once per cooldown window (C05); the CP/EXP/drop side of a PvP kill's reward is still not
    ///     implemented.
    /// </summary>
    public int MissionKillOtherTribe { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aKillMonster</c> -- tracked (echoed on ZC 163) but its own claim-gate is compiled out
    ///     in EU33, so it never blocks a claim.
    /// </summary>
    public int MissionKillMonster { get; set; }

    /// <summary>
    ///     Legacy <c>aMissionDate.aPlayTime</c> -- same "tracked, gate compiled out" posture as
    ///     <see cref="MissionKillMonster" />.
    /// </summary>
    public int MissionPlayTime { get; set; }

    /// <summary>Legacy <c>aAutoState</c> (0/1) -- CZ_AUTO_CONFIG_SEND/ZC_AUTO_CONFIG_RECV (opcode 99/123).</summary>
    public bool AutoHuntEnabled { get; set; }

    /// <summary>
    ///     The raw 112-byte AUTO_HUNT blob, copied verbatim from the client with no server-side content
    ///     validation -- matches the legacy exactly (an anti-cheat surface deliberately left open). Null =
    ///     never configured. The autonomous bot loop itself is out of scope for this pass; only the
    ///     config-storage/gating half is implemented.
    /// </summary>
    public AutoHunt? AutoHuntConfig { get; set; }

    /// <summary>Legacy <c>aAutoLifeRatio</c> (0-5) -- CZ_CHANGE_AUTO_INFO, silently stored, never echoed back.</summary>
    public byte AutoLifeRatio { get; set; }

    /// <summary>Legacy <c>aAutoManaRatio</c> (0-5) -- same posture as <see cref="AutoLifeRatio" />.</summary>
    public byte AutoManaRatio { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last CZ_HERORANK_INFO_SEND reply for the previous period (ZC 148) --
    ///     2.5s per-user throttle. Null = never queried yet.
    /// </summary>
    public TimeSpan? LastHeroRankingPreviousQueryAtZoneClock { get; set; }

    /// <summary>
    ///     Same throttle posture as <see cref="LastHeroRankingPreviousQueryAtZoneClock" />, for the current period (ZC
    ///     150).
    /// </summary>
    public TimeSpan? LastHeroRankingCurrentQueryAtZoneClock { get; set; }

    /// <summary>
    ///     A Fenrir simplification of the legacy's per-pet-item growth counter -- tracked per character
    ///     instead of per item instance. Reset to the newly-equipped pet's base tier whenever the Equipment
    ///     container's pet slot changes to a different item id.
    /// </summary>
    public int PetGrowth { get; set; }

    /// <summary>
    ///     0-100 activity -- decays -1 every 30s while a pet is equipped and not already at 0. Gates the
    ///     attack contribution only; Life/Mana/Defense contributions do NOT gate on activity (verified).
    /// </summary>
    public byte PetActivity { get; set; }

    /// <summary>
    ///     The ItemId last seen equipped in the pet slot -- lets <see cref="World.Zone" /> detect a pet swap (not just
    ///     any equipment change) to reset <see cref="PetGrowth" />/<see cref="PetActivity" />. 0 = no pet equipped.
    /// </summary>
    public int LastSeenPetItemId { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.PetActivitySystem" />'s own 30s decay cadence -- never read
    ///     by anything else.
    /// </summary>
    public int PetActivityDecayTicks { get; set; }

    /// <summary>
    ///     True while this character has a live personal-shop stall open. Deliberately not set for a
    ///     proxy/offline shop (that state lives in game.OfflineShops instead, since it must keep working while
    ///     this character is offline). A seller's copy is only ever mutated by a different character's (the
    ///     buyer's) request thread through <see cref="Social.Pshop.PshopZoneCommand" />, routed onto the
    ///     seller's own zone tick.
    /// </summary>
    public bool PshopOpen { get; set; }

    /// <summary>
    ///     The currently-advertised stall listing while <see cref="PshopOpen" /> is true; stale/meaningless otherwise
    ///     (not cleared on close).
    /// </summary>
    public PshopInfo? PshopListing { get; set; }

    /// <summary>
    ///     Legacy <c>mDATA.mFishingState</c> (0/1), zone 52 only. Session-only: the legacy itself keeps this on
    ///     <c>MyUser</c>, not <c>AVATAR_INFO</c>, so no persisted column exists or is needed.
    /// </summary>
    public int FishingState { get; set; }

    /// <summary>Legacy <c>mDATA.mFishingStep</c> (0..5). Same session-only posture as <see cref="FishingState" />.</summary>
    public int FishingStep { get; set; }

    /// <summary>
    ///     Legacy <c>mFishingTickCount</c> -- gates the ~1-minute poll-bite window (CZ_FISHING_RESULT_SEND Sort=1).
    ///     Deliberately wall-clock (unlike most other <c>*AtZoneClock</c> fields): the elapsed check is resolved
    ///     on the request thread reading this field directly, which cannot see Zone's private simulated clock.
    ///     Null = never cast.
    /// </summary>
    public DateTime? FishingCastAtUtc { get; set; }

    /// <summary>
    ///     Legacy <c>mCatchingFish</c> -- true only after a bite (step 4/5), gates CZ_FISHING_REWARD_SEND. Cleared
    ///     unconditionally by every CZ_FISHING_STATE_SEND (cast/reel), matching the legacy exactly.
    /// </summary>
    public bool CatchingFish { get; set; }

    /// <summary>
    ///     aAnimal[10] mount garage -- no acquisition path exists yet (only the unimplemented UseInventoryItem
    ///     mount-item family populates it per S04_MyWork03.cpp), so every slot stays 0 until that lands. A real
    ///     array, permanently empty for now, same posture as <see cref="MissionJoinWar" />.
    /// </summary>
    public ImmutableArray<int> MountGarage { get; set; } = ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    ///     aAnimalIndex. -1 = none selected, 0-9 = selected garage slot, 10-19 = actively mounted (slot + 10, legacy's
    ///     own offset encoding).
    /// </summary>
    public int AnimalIndex { get; set; } = -1;

    /// <summary>aAnimalNumber -- the currently-mounted animal id feeding combat stats/broadcast. 0 = not mounted.</summary>
    public int AnimalNumber { get; set; }

    /// <summary>aAnimalAbsorbState (0/1) -- CZ_ANIMAL_ABSORB_SEND toggle.</summary>
    public int AnimalAbsorbState { get; set; }

    /// <summary>
    ///     aAnimalTime -- gates CZ_ANIMAL_STATE_SEND case 3 (mount), &gt;= 1 required. Same "real but currently
    ///     unreachable" posture as <see cref="MissionJoinWar" />: no acquisition path exists yet, so this stays 0.
    /// </summary>
    public int AnimalTime { get; set; }

    /// <summary>
    ///     aAnimalAbsorbTime -- gates CZ_ANIMAL_ABSORB_SEND case 1 (enable absorb), &gt;= 1 required. Same posture as
    ///     <see cref="AnimalTime" />.
    /// </summary>
    public int AnimalAbsorbTime { get; set; }

    /// <summary>
    ///     aCostume[10] wardrobe -- same "no acquisition path yet" posture as <see cref="MountGarage" />. A
    ///     slot's occupancy is simplified here to "non-zero" rather than replicating the legacy's ~300-entry
    ///     IsValidCostume item-id whitelist (Server/Header/function.h): no code path grants a costume yet, so
    ///     the simplification never diverges from that table in practice today.
    /// </summary>
    public ImmutableArray<int> CostumeWardrobe { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    ///     aCostumeIndex. Same offset-encoding convention as <see cref="AnimalIndex" /> (-1 none, 0-9 selected, 10-19
    ///     worn).
    /// </summary>
    public int CostumeIndex { get; set; } = -1;

    /// <summary>aCostumeNumber -- the currently-worn costume id. 0 = none worn.</summary>
    public int CostumeNumber { get; set; }

    /// <summary>aCostumeState -- CZ_COSTUME_STATE2_SEND visibility toggle (0/1).</summary>
    public int CostumeState { get; set; }

    /// <summary>
    ///     aAction.aPetSort -- last accepted CZ_UPDATE_PET_ACTION_SEND's pet sub-fields. The legacy handler has
    ///     no reply/broadcast of its own; the update rides along on the next full avatar rebroadcast instead
    ///     (<see cref="Zone" />'s shared rebroadcast-snapshot builder still needs wiring to surface these --
    ///     tracked here, not yet plumbed into that shared path).
    /// </summary>
    public int PetActionSort { get; set; }

    public float PetActionFront { get; set; }
    public float PetActionLocationX { get; set; }
    public float PetActionLocationY { get; set; }
    public float PetActionLocationZ { get; set; }
    public float PetActionTargetLocationX { get; set; }
    public float PetActionTargetLocationY { get; set; }
    public float PetActionTargetLocationZ { get; set; }

    /// <summary>
    ///     aAutoBuffSkill[8] -- CZ_CONTINUE_SKILL_STAT_SEND (op94) registered auto-buff (skillId, grade) slots.
    ///     Session-scoped only, same "no persisted column yet" posture as <see cref="MountGarage" />.
    /// </summary>
    public ImmutableArray<(int SkillId, int Grade)> AutoBuffSkill { get; set; } = DefaultAutoBuffSkill;

    /// <summary>
    ///     aAutoBuffTime (YYYYMMDD, <see cref="Simulation.GameDate" /> encoding) -- gates CZ_CONTINUE_SKILL_USE_SEND
    ///     Sort=1. Same "real but currently unreachable" posture as <see cref="AnimalAbsorbTime" />: no
    ///     acquisition path (the unimplemented UseInventoryItem cash-boost family) exists yet, so this stays 0.
    /// </summary>
    public int AutoBuffTime { get; set; }

    /// <summary>
    ///     aStateTimeEffect -- CZ_TIME_EFFECT_SEND (op97) reward tier currently applied (120/180/240/300/360), 0 =
    ///     none. SetTimeEffect's downstream drop/exp-rate multipliers (mItemDropUpRatio etc.) are not modeled --
    ///     see <see cref="Handlers.PlaytimeBuffHandler" />'s remarks.
    /// </summary>
    public int StateTimeEffect { get; set; }

    /// <summary>
    ///     aRankBuffType -- CZ_RANK_BUFF_SEND (op111) active buff tier (1-7), 0 = none. MyFactor.cpp's per-tier
    ///     stat bonuses are not modeled yet -- see <see cref="Handlers.RankBuffHandler" />'s remarks.
    /// </summary>
    public int RankBuffType { get; set; }

    /// <summary>
    ///     aRuneSystem[4] -- ItemId currently socketed per rune slot (93514-93517 family, index-aligned), 0 =
    ///     empty. Session-only, no persisted column exists yet -- same posture as <see cref="MountGarage" />.
    /// </summary>
    public ImmutableArray<int> RuneSystem { get; set; } = [0, 0, 0, 0];

    /// <summary>
    ///     aRuneSystemStat[4] -- the socketed rune's raw packed enchant/combine/refine/socket int (<c>ItemValueCodec</c>
    ///     -encoded), paired 1:1 with <see cref="RuneSystem" />.
    /// </summary>
    public ImmutableArray<int> RuneSystemStat { get; set; } = [0, 0, 0, 0];

    /// <summary>
    ///     aStellarCore[10] wardrobe -- same "no acquisition path yet" posture as <see cref="CostumeWardrobe" />:
    ///     no code path grants a stellar core yet, so every slot stays 0 until that lands.
    /// </summary>
    public ImmutableArray<int> StellarCoreWardrobe { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    ///     aStellarCoreIndex. Same offset-encoding convention as <see cref="CostumeIndex" /> (-1 none, 0-9 selected,
    ///     10-19 worn).
    /// </summary>
    public int StellarCoreIndex { get; set; } = -1;

    /// <summary>aStellarCoreNumber -- the currently-worn stellar core id feeding stats/broadcast. 0 = none worn.</summary>
    public int StellarCoreNumber { get; set; }
}
