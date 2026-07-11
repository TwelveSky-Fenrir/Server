using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
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

    /// <summary>
    ///     True from <see cref="Zone.ApplyDeath" /> until <see cref="Zone.GrantReviveEligibility" />'s
    ///     territorial recheck succeeds. Distinct from the life-value-based "is dead" predicate used elsewhere
    ///     in combat (<c>H07_MyGame.h:969 IsDeath</c>).
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    ///     Legacy ticks elapsed since <see cref="Zone.ApplyDeath" /> -- the single shared stamp
    ///     <see cref="Simulation.DeathGateTickSystem" /> measures its 10/30/50-tick thresholds against.
    ///     Meaningless while <see cref="IsDead" /> is false (reset to 0 on every death and on every eligibility
    ///     grant).
    /// </summary>
    public int TicksSinceDeath { get; set; }

    /// <summary>
    ///     <c>mProtect_ReviveHack</c> -- armed at death for every cause except a private duel
    ///     (<see cref="DeathCause.Duel" />), cleared by <see cref="Zone.GrantReviveEligibility" />. While true
    ///     past <see cref="Simulation.SimulationClock.AntiAbuseForceQuitLegacyTicks" />, the session is
    ///     force-quit; while true past <see cref="Simulation.SimulationClock.DeathBroadcastSuppressionLegacyTicks" />,
    ///     this character stops receiving proximity-broadcast avatar traffic; a premature "stand up" action
    ///     request or a zone-transfer attempt while this is set also kicks the session outright -- see
    ///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.AvatarActionService</c>/<c>ZoneMoveService</c>.
    /// </summary>
    public bool ReviveHackFlag { get; set; }

    /// <summary>
    ///     Legacy <c>mMoveZoneResult</c> / <c>MyUser::IsMovingZone()</c> (<c>Server/ts25zone/H07_MyGame.h:846</c>,
    ///     <c>Server/ts25zone/H03_MyUser.h:15</c>, <c>return mMoveZoneResult == 1;</c>) -- true from the instant
    ///     this shard tells the client the destination zone's IP/port for a CROSS-SHARD zone transfer (set by
    ///     <c>Zone.HandleMarkZoneTransferPending</c>, posted via
    ///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.ZoneMoveService.HandleCrossShardAsync</c>'s
    ///     successful-handoff branch, <c>Server/ts25zone/S04_MyWork02.cpp:2182</c>'s Fenrir analog) until this
    ///     character's actual disconnect from THIS shard finally removes it from <see cref="Zone" />'s own
    ///     player map.
    ///     <para>
    ///         Deliberately never set for a SAME-shard handoff (<c>Zone.HandleLeave</c>'s non-null
    ///         <c>handoffTarget</c> branch): that path removes the character from the zone's player map and
    ///         posts the destination zone's Enter atomically in the same tick, so there is no observable
    ///         "moving" window to gate against there. Only the cross-shard path
    ///         (<c>ZoneMoveService.HandleCrossShardAsync</c>) leaves the transferring character live and
    ///         trackable in the ORIGIN shard's own zone for the whole real-world interval until the client's
    ///         own TCP disconnect actually happens -- exactly the window legacy's <c>mMoveZoneResult</c> was
    ///         built to guard. See the zone-transfer-in-progress-gate behavior contract (2026-07 round) for
    ///         the full citation trail.
    ///     </para>
    ///     <para>
    ///         Wired-up consumers so far: <c>Monsters.MonsterAiSystem.TryAcquireTarget</c> (excludes a
    ///         flagged avatar from monster target acquisition, mirroring <see cref="IsDead" />'s own
    ///         exclusion), <c>Combat.UnstunResolver.Resolve</c> (via
    ///         <see cref="Combat.CombatantSnapshot.IsMovingZone" /> on the cure attempt's target only, never
    ///         the curer -- matching legacy exactly), <c>Buffs.RankBuffResolver.Resolve</c> (the ACTING
    ///         player's own state, unlike the two consumers above -- CZ_RANK_BUFF_SEND's legacy
    ///         <c>IsMovingZone()</c> gate checks the requester, not a target; see that resolver's own
    ///         remarks), and <c>Social.GuildInviteService.AskAsync</c> (the target's own state, alongside
    ///         <see cref="IsStunned" />/<see cref="IsDead" />, target-only -- matching
    ///         <c>UnstunResolver</c>'s pattern, re-verified 2026-07-11). The remaining combat/social sites the
    ///         contract's own citation range covers (<c>S04_MyWork02.cpp:8292-9950</c>, minus the sites
    ///         already re-verified there) are deliberately left unwired here -- an open, tracked follow-up,
    ///         not guessed at.
    ///     </para>
    /// </summary>
    public bool IsMovingZone { get; set; }

    /// <summary>
    ///     Legacy internal death sub-counter -- set to <see cref="ReviveEligibilityRules.DeathSubCounterBaseline" />
    ///     at death and again on eligibility grant. No consumer of it was located in the cited files (see the
    ///     behavior contract this satisfies), so its downstream purpose is not modeled here.
    /// </summary>
    public int DeathSubCounter { get; set; }

    /// <summary>
    ///     Mirrors the legacy's own persistent <c>mDATA.aAction.aSort</c> -- the last accepted avatar action's
    ///     Sort, updated by <see cref="Zone.HandleMove" /> alongside position for every action, not just
    ///     movement. Read by <see cref="Simulation.MeditationRegenSystem" /> (31 = sitting/meditating). 0 = idle.
    /// </summary>
    /// <remarks>
    ///     RESOLVED (previously logged here as an unreconciled "KNOWN CONFLICT" between
    ///     <see cref="CharacterMotionWhitelist" />'s and <see cref="Simulation.MeditationRegenSystem" />'s own
    ///     citations): both citations are correct, because they describe two textually distinct, separately
    ///     gated legacy switch statements, not one contradictory one. <see cref="CharacterMotionWhitelist" />
    ///     reimplements CZ_AVATAR_ACTION_SEND (op15)'s own motion-legality table
    ///     (<c>CheckValidCharacterMotionForSend</c>, Server/ts25zone/S04_MyWork05.cpp:4249-4252), where Sort
    ///     31 is reachable only under <c>#ifdef __MOBILE__</c> -- never <c>#define</c>d anywhere in the whole
    ///     <c>Server/</c> tree, so dead code via op15 in every shipped build.
    ///     <see cref="Movement.AvatarActionResumeWhitelist" />
    ///     reimplements CZ_UPDATE_AVATAR_ACTION (op16)'s own, entirely separate inline switch
    ///     (Server/ts25zone/S04_MyWork02.cpp:1789-1878), which legalizes Sort 31 with Type 0 unconditionally
    ///     and with no preprocessor guard at all (:1834-1840, disconnecting instead on any other Type) -- live
    ///     in every build. <see cref="Zone.HandleMove" /> (Zone.PlayerLifecycle.cs:613-639) dispatches to
    ///     whichever of the two whitelists matches the opcode that delivered the action, so a real client CAN
    ///     and does reach Sort 31 here, via a live op16 packet -- only the op15 path is dead.
    ///     <see cref="Simulation.MeditationRegenSystem" /> already reads this field correctly as-is; no code
    ///     change was needed here, only this comment (and <c>MeditationRegenSystemTests</c>' three previously
    ///     skipped tests, now enabled) were stale.
    ///     <para>
    ///         Deliberately left at the implicit C# default of 0, NOT defaulted to 1 -- this field does double
    ///         duty as (a) the ATTACKER's own replay-guard consistency value (
    ///         <see cref="Combat.AttackPacketBudget.TryConsume" />
    ///         compares the incoming sub-packet's <c>AttackActionValue4</c> against this field, and a real
    ///         client's own attack packet is always preceded by the op15/op16 action that just set this to the
    ///         SAME value moments earlier) and (b) the DEFENDER's action-state precondition
    ///         (<see cref="Combat.CombatResolver.ResolveEnemyTribeAttack" />'s <c>NoActionYetSort</c>/
    ///         <c>DeathPoseSort</c> gate). Changing this property's default to close (b) silently broke (a) for
    ///         every existing test/scenario whose recorded <c>AttackActionValue4</c> assumed the plain-0
    ///         default -- confirmed by a 39-test regression when tried. The (b) gap is real (a freshly-entered
    ///         defender who hasn't sent their own first action yet defaults here to 0 = <c>NoActionYetSort</c>,
    ///         making them briefly untargetable) but is closed at the TEST-FIXTURE level instead (each combat
    ///         test's defender explicitly sets a legal ActionSort before being attacked, matching
    ///         <c>ZoneDuelCombatTests</c>' own established pattern) rather than by changing this shared
    ///         default's blast radius onto the unrelated attacker-side invariant.
    ///     </para>
    /// </remarks>
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
    ///     Mirrors the legacy's <c>mCheckMaxAttackPacketNum</c> -- whether <see cref="AttackSubPacketCeiling" />
    ///     is enforced at all for the character's current action. Set on every accepted op15
    ///     (CZ_AVATAR_ACTION_SEND) action by <see cref="CharacterMotionWhitelist" /> (<see cref="Zone.HandleMove" />);
    ///     pre-set to true here (enforced, ceiling zero) so a character that has never yet had an action
    ///     accepted starts in the same deny-all-attacks state the legacy session itself starts in
    ///     (Server/ts25zone/S04_MyWork02.cpp:855-869), not merely the whitelist's own per-call fallback. Left
    ///     untouched by an op16 (CZ_UPDATE_AVATAR_ACTION) action: op16's own legacy handler body never
    ///     computes or reads this concept at all (see <see cref="Movement.AvatarActionResumeWhitelist" />'s
    ///     own remarks), so overwriting it from an op16 lookup would not be legacy-accurate.
    /// </summary>
    public bool AttackBudgetEnforced { get; set; } = true;

    /// <summary>
    ///     Mirrors the legacy's <c>mAttackPacketSort</c> -- which family (0-5) of attack-resolution sub-packet
    ///     the character's current action expects. Recorded for reuse elsewhere; neither
    ///     <see cref="CharacterMotionWhitelist" /> nor <see cref="AttackPacketBudget" />
    ///     interprets this value themselves.
    /// </summary>
    public int AttackFamilyTag { get; set; }

    /// <summary>
    ///     Mirrors the legacy's <c>mMaxAttackPacketNum</c> -- the exact number of attack-resolution sub-packets
    ///     permitted for the character's current action before a fresh avatar action (and whitelist
    ///     re-evaluation) must arrive. Meaningless while <see cref="AttackBudgetEnforced" /> is false.
    /// </summary>
    public int AttackSubPacketCeiling { get; set; }

    /// <summary>
    ///     Mirrors the legacy's <c>mNowAttackPacketNum</c> -- attack-resolution sub-packets already consumed
    ///     for the character's current action. Reset to 0 by every accepted op15 (CZ_AVATAR_ACTION_SEND)
    ///     action (<see cref="Zone.HandleMove" />), incremented by <see cref="AttackPacketBudget.TryConsume" />.
    ///     Left untouched by an op16 (CZ_UPDATE_AVATAR_ACTION) action, for the same reason as
    ///     <see cref="AttackBudgetEnforced" />.
    /// </summary>
    public int AttackSubPacketsUsed { get; set; }

    /// <summary>
    ///     Live BUFF_INFO mirror (35 slots x [value, duration-in-legacy-ticks]) -- fed to
    ///     <see cref="Stats.StatCalculator.ComputeEffectiveStats" /> and decremented/expired by
    ///     <see cref="Simulation.BuffExpirySystem" /> every legacy tick.
    /// </summary>
    /// <remarks>
    ///     Deliberately a fresh per-instance array (never
    ///     <c>Fenrir.Network.Serialization.Shared.Packets.Shared.WorldStateTemplates.ZeroedBuffInfo</c>,
    ///     a shared static instance) -- reusing that template would let every player's buffs alias the same backing
    ///     <c>int[]</c>.
    /// </remarks>
    public BuffInfo Buffs { get; } = new() { Buff = new int[70] };

    /// <summary>
    ///     Reusable 35-slot "changed slot" mask scratch buffer, shared by every buff-write-and-notify call
    ///     site for THIS player (<see cref="Zone.ApplyBuffWrites" /> for a manual op30 self-buff cast and the
    ///     auto-hunt bot-buff loop, <see cref="Simulation.BuffExpirySystem" />'s natural per-tick expiry, and
    ///     <see cref="Zone.ClearAllBuffs" /> on death) instead of each one allocating its own fresh
    ///     <c>int[35]</c> on the managed heap per call -- see the buff-slot-write-and-notify-pattern behavior
    ///     contract. Safe to reuse across calls: every writer is this player's own zone tick thread (this
    ///     class's own single-writer contract), and <see cref="Zone.RecomputeStatsAndBroadcastBuffs" />
    ///     synchronously serializes the array's contents into the outgoing wire frame before returning, so no
    ///     caller retains a reference to it past that call. Every writer clears (or lazily clears on first
    ///     actual change) this buffer itself before flagging slots, since a stale flag from a PRIOR unrelated
    ///     call on this same player must never leak into the next notification.
    /// </summary>
    public int[] BuffChangeScratch { get; } = new int[35];

    /// <summary>
    ///     Zone-clock instant of this character's mutual PvP-attack-immunity window start -- the legacy's
    ///     anti-chain-attack grace window (~10s, <c>PROTECT_TICK</c>, checked on both sides of an attack:
    ///     <see cref="Combat.CombatResolver" />/<see cref="Combat.MonsterCombatResolver" />/
    ///     <see cref="Combat.StunResolver" />). Exactly two legitimate write sites, matching legacy's own two
    ///     (<c>S04_MyWork02.cpp:838</c> and <c>:1783</c>): zone (re)entry (<see cref="Zone.HandleEnter" />) and
    ///     every accepted Sort-0 ("rest"/stand-up) CZ_AVATAR_ACTION_SEND action
    ///     (<see cref="Zone.ApplyRestActionProtectionAndHeal" />). Never refreshed by taking/dealing damage: it
    ///     is a one-shot grace period re-armed only by those two events, not a rolling cooldown (an earlier
    ///     version here refreshed it on every hit, which made two players who traded blows mutually unable to
    ///     fight anyone for the next 10s). Null, not <see cref="TimeSpan.Zero" />, means "never entered" --
    ///     zero is itself a reachable zone-clock instant.
    /// </summary>
    public TimeSpan? ZoneEntryAtZoneClock { get; set; }

    /// <summary>
    ///     Zone-clock instant of this character's last accepted skill-cast mana charge (op15
    ///     CZ_AVATAR_ACTION_SEND, action-category Sort resolving to the real skill-cast category -- Sorts
    ///     32, 33, 38-90 -- NOT action-Sort 30, which is the unrelated stand-up-from-death request) -- a
    ///     global, one-cast-attempt-per-legacy-tick anti-flood gate. Null means "never cast." See
    ///     <see cref="Zone.ApplySkillCastManaCharge" /> and the skill-casting-cooldown-mechanics behavior
    ///     contract.
    /// </summary>
    public TimeSpan? LastSkillCastAtZoneClock { get; set; }

    /// <summary>
    ///     <c>mLastHSTick</c> -- real-time (not simulated-clock) reapplication cooldown for skill 82's
    ///     ("Holy Shield", <see cref="Skills.SkillEffectCatalog" /> slot 9) timed buff, enforced only on the
    ///     zone-server process specifically assigned server number 124 at startup
    ///     (<see cref="World.Zone.MapId" /> == 124 in Fenrir's one-process-per-map model). Compared against a
    ///     genuine wall-clock read (legacy's own <c>GetTickCount()</c>-style real elapsed milliseconds), NOT
    ///     <see cref="LastSkillCastAtZoneClock" />'s Zone-simulated clock -- a deliberately different clock
    ///     source from every other same-tick guard on this type. Initialized to <see cref="DateTime.MinValue" />
    ///     (not <see cref="DateTime.UtcNow" />), mirroring legacy's own zero-init at avatar registration
    ///     (S04_MyWork02.cpp:873): the very first skill-82 application on a zone-124 process must always clear
    ///     the ten-second threshold, unlike <see cref="LastEnchantAttemptUtc" />'s opposite "already now"
    ///     default. Breaching the cooldown is a pure no-op skip (buff slot 9 left unpopulated for that pass) --
    ///     no disconnect, no counter escalation, unlike every other guard in this set.
    /// </summary>
    public DateTime LastHolyShieldAppliedUtc { get; set; } = DateTime.MinValue;

    /// <summary>
    ///     mCheckStun / aAction.aSort==11 -- true while stunned (ProcessAttack05/<see cref="Combat.StunResolver" />).
    ///     Vetoes every other client-requested action while true
    ///     (<see cref="Zone.HandleMove" />, <c>W_AVATAR_ACTION_SEND</c>'s anti-stun-hack veto,
    ///     <c>S04_MyWork02.cpp:1293,1329-1338</c>) and is cleared either by a successful cure
    ///     (<see cref="Combat.UnstunResolver" />) or by <see cref="Simulation.StunCountdownSystem" />'s own
    ///     natural expiry.
    /// </summary>
    public bool IsStunned { get; set; }

    /// <summary>
    ///     aAction.aSkillValue while stunned -- remaining stun duration in ~1-second units, decremented once
    ///     every <see cref="Simulation.SimulationClock.StunCountdownLegacyTicks" /> (~1 s) by
    ///     <see cref="Simulation.StunCountdownSystem" />, NOT once per legacy tick like an ordinary
    ///     <see cref="Buffs" /> slot. Meaningless while <see cref="IsStunned" /> is false.
    /// </summary>
    public int StunDurationSeconds { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator toward the next ~1s stun-duration decrement -- same per-character-accumulator
    ///     pattern as <see cref="PetActivityDecayTicks" /> for a sub-legacy-tick-rate cadence, consumed only by
    ///     <see cref="Simulation.StunCountdownSystem" />.
    /// </summary>
    public int StunCountdownAccumulatorTicks { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator toward the next ~1s sit/meditate HP+MP regen firing -- same
    ///     per-character-accumulator pattern as <see cref="StunCountdownAccumulatorTicks" /> for the identical
    ///     shared gate (<c>S07_MyGame04.cpp:378-380</c>), consumed only by
    ///     <see cref="Simulation.MeditationRegenSystem" />. Only accumulates while
    ///     <see cref="ActionSort" /> == 31 (sitting); a critical-severity fix -- without this gate the regen
    ///     system applied a full ~1-second regen amount on every 500 ms legacy tick, twice the legacy rate.
    /// </summary>
    public int MeditationRegenAccumulatorTicks { get; set; }

    /// <summary>
    ///     mStunAtkCount -- the team-stun (skill 80) sub-mechanic's repeated-stun counter on the victim side.
    ///     Reset to 0 by a successful cure or natural stun expiry (<c>S07_MyGame04.cpp:2593-2612</c>); forces a
    ///     stun-lock death (<see cref="Zone.ApplyDeath" />) once it reaches 10 (<c>S07_MyGame02.cpp:3727-3731</c>).
    /// </summary>
    public int RepeatedStunCount { get; set; }

    /// <summary>
    ///     mCheckPossibleEatPotion -- shared by two independent writers: false while <see cref="IsStunned" /> is
    ///     true (restored on cure/expiry), and false while <see cref="IsDead" /> is true (cleared by
    ///     <see cref="Zone.ApplyDeath" />, restored by <see cref="Zone.GrantReviveEligibility" />,
    ///     <c>S04_MyWork02.cpp:2258-2330</c>). No consumable/potion-use system reads this yet
    ///     (<see cref="Simulation.AutoHuntTickSystem" />'s own remarks note the same open gap for
    ///     <c>BotHotKey</c>'s potion refill); tracked here so that system has this to gate on once it exists.
    ///     Neither writer is expected to interact with the other today: a dead character isn't concurrently
    ///     modeled as stunned.
    /// </summary>
    public bool CanUseConsumables { get; set; } = true;

    /// <summary>
    ///     A per-user anti-cheat toggle gating the "verify echoed animation state" re-check inside
    ///     <c>ProcessAttack05</c>/<c>06</c> (<c>S07_MyGame02.cpp:3593-3600</c>,<c>:3783-3798</c>) -- distinct
    ///     from, and narrower than, the packet self-origin/rate-cap check <c>CheckAttackPacket</c> performs
    ///     (which stun's own call site disables outright, <c>:3527</c>). No code path in Fenrir sets this true
    ///     yet -- same "real but currently unreachable" posture as <see cref="MissionJoinWar" />/
    ///     <see cref="AnimalTime" />: <see cref="Combat.StunResolver" />/<see cref="Combat.UnstunResolver" />'s
    ///     echo check only ever short-circuits to "pass" until something flips this on.
    /// </summary>
    public bool VerifyEchoedActionState { get; set; }
}
