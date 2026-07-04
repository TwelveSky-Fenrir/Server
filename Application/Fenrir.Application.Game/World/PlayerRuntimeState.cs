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

    /// <summary>aRebirthNum (cap 12) -- read by StatCalculator's CriticalDefence and Critical wrapper bonuses.</summary>
    public int RebirthCount { get; set; }

    /// <summary>aTitle (category*100 + rank 1-14) -- read by StatCalculator's title-rank bonus tables.</summary>
    public int Title { get; set; }

    /// <summary>aHalo -- read twice independently by StatCalculator: added directly to all 4 base stats, and again for its own CriticalDefence bonus.</summary>
    public int Halo { get; set; }

    /// <summary>aKillOtherTribe (CP) -- quest reward type 3 income; not consumed by StatCalculator.</summary>
    public int ContributionPoints { get; set; }

    /// <summary>aTeacherPoint -- quest reward type 5 income. A separate counter from the Mentor system's TeacherCharacterId/StudentCharacterId bond.</summary>
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

    /// <summary>AOI grid bookkeeping -- which cell this player currently occupies, so <see cref="AoiGrid" /> can detect a crossing without a full rescan.</summary>
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
    ///     Deliberately a fresh per-instance array (never <c>Fenrir.Contracts.Packets.Shared.WorldStateTemplates.ZeroedBuffInfo</c>,
    ///     a shared static instance) -- reusing that template would let every player's buffs alias the same backing <c>int[]</c>.
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

    /// <summary>Cosmetic in-guild title (legacy gMemberCall, game.GuildMembers.CallName) -- loaded once at world entry alongside <see cref="GuildId" />.</summary>
    public string GuildCallName { get; set; } = "";

    /// <summary>
    ///     This character's tribe role -- loaded once at world entry, matching <c>ReturnTribeRole</c>'s own
    ///     encoding directly (0 = regular, 1 = master, 2 = sub-master).
    /// </summary>
    public byte TribeRole { get; set; }

    /// <summary>aUseOrnament. Session-scoped only -- not yet loaded from/flushed to game.Characters (no persisted column exists yet).</summary>
    public bool UseOrnament { get; set; }

    /// <summary>aProtectForHalo -- a consumable charge that absorbs one "halo -1" downgrade. Same open issue as <see cref="UseOrnament" />: session-scoped only.</summary>
    public int ProtectForHalo { get; set; }

    /// <summary>aBonusItemLevel -- which level-up milestone's bonus-item claim is pending. Session-scoped only; always 0 until a leveling-milestone system grants it.</summary>
    public int BonusItemLevel { get; set; }

    /// <summary>aBonusItemValue -- companion flag to <see cref="BonusItemLevel" />, same open issue.</summary>
    public bool BonusItemValue { get; set; }

    /// <summary>
    ///     aPreviousTribe. No rebirth/tribe-transition system exists yet to populate this from anything other
    ///     than the character's current <see cref="Tribe" /> -- defaults to <see cref="Tribe" /> at world entry.
    /// </summary>
    public byte PreviousTribe { get; set; }

    /// <summary>
    ///     This character's own friend list (slot -&gt; friend CharacterId), mutated directly by friend-add/
    ///     remove handlers on the request thread -- a deliberate exception to the single-writer invariant. A
    ///     zone-transfer handoff carries this same dictionary instance to the target zone and enumerates it on
    ///     that zone's own tick thread, so a concurrent Add/Remove must not throw during enumeration --
    ///     <see cref="ConcurrentDictionary{TKey,TValue}" /> makes that race safe.
    /// </summary>
    public ConcurrentDictionary<byte, int> Friends { get; } = new();

    /// <summary>This character's teacher (master), if any -- mutated live by mentor start/end handlers (same request-thread exception as <see cref="Friends" />). Null = no teacher.</summary>
    public int? TeacherCharacterId { get; set; }

    /// <summary>This character's student, if any (only meaningful for a master) -- same posture as <see cref="TeacherCharacterId" />.</summary>
    public int? StudentCharacterId { get; set; }

    /// <summary>The linear per-tribe quest chain's permanent progression index (legacy <c>aQuestInfo[0]</c>) -- survives completion/abandon.</summary>
    public int QuestStepPermanent { get; set; }

    /// <summary>Legacy <c>aQuestInfo[1]</c> -- a 0/1 "quest active" flag, NOT a quest id despite the DB column's legacy-derived name.</summary>
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
    ///     gates the daily-mission claim (&gt;= 10). Same "real but currently unreachable" posture as
    ///     <see cref="MissionJoinWar" />: its increment hook (PvP-kill CP/XP pipeline) is not implemented.
    /// </summary>
    public int MissionKillOtherTribe { get; set; }

    /// <summary>Legacy <c>aMissionDate.aKillMonster</c> -- tracked (echoed on ZC 163) but its own claim-gate is compiled out in EU33, so it never blocks a claim.</summary>
    public int MissionKillMonster { get; set; }

    /// <summary>Legacy <c>aMissionDate.aPlayTime</c> -- same "tracked, gate compiled out" posture as <see cref="MissionKillMonster" />.</summary>
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

    /// <summary>Zone-clock instant of this character's last CZ_HERORANK_INFO_SEND reply for the previous period (ZC 148) -- 2.5s per-user throttle. Null = never queried yet.</summary>
    public TimeSpan? LastHeroRankingPreviousQueryAtZoneClock { get; set; }

    /// <summary>Same throttle posture as <see cref="LastHeroRankingPreviousQueryAtZoneClock" />, for the current period (ZC 150).</summary>
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

    /// <summary>The ItemId last seen equipped in the pet slot -- lets <see cref="World.Zone" /> detect a pet swap (not just any equipment change) to reset <see cref="PetGrowth" />/<see cref="PetActivity" />. 0 = no pet equipped.</summary>
    public int LastSeenPetItemId { get; set; }

    /// <summary>Legacy-tick accumulator for <see cref="Simulation.PetActivitySystem" />'s own 30s decay cadence -- never read by anything else.</summary>
    public int PetActivityDecayTicks { get; set; }

    /// <summary>
    ///     True while this character has a live personal-shop stall open. Deliberately not set for a
    ///     proxy/offline shop (that state lives in game.OfflineShops instead, since it must keep working while
    ///     this character is offline). A seller's copy is only ever mutated by a different character's (the
    ///     buyer's) request thread through <see cref="Social.Pshop.PshopZoneCommand" />, routed onto the
    ///     seller's own zone tick.
    /// </summary>
    public bool PshopOpen { get; set; }

    /// <summary>The currently-advertised stall listing while <see cref="PshopOpen" /> is true; stale/meaningless otherwise (not cleared on close).</summary>
    public PshopInfo? PshopListing { get; set; }
}
