using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Network.Serialization.Packets.Shared;

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
    ///     KNOWN CONFLICT (discovered wiring <see cref="CharacterMotionWhitelist" />, not yet
    ///     reconciled): <see cref="CharacterMotionWhitelist" />'s own citation
    ///     (Server/ts25zone/S04_MyWork05.cpp:4249-4252) confirms Sort 31 is reachable only under
    ///     <c>#ifdef __MOBILE__</c>, never defined in any shipped build -- i.e. dead code, so no legally
    ///     accepted avatar action can ever set this field to 31 once <see cref="Zone.HandleMove" /> enforces
    ///     that whitelist. <see cref="Simulation.MeditationRegenSystem" />'s own citation
    ///     (S07_MyGame04.cpp:461-518) independently asserts aSort==31 is a real, reachable meditation state.
    ///     These two legacy-grounded citations contradict each other; resolving which one is right (and what
    ///     Sort/Type the real client actually sends to sit/meditate) needs a fresh
    ///     legacy-behavior-translator contract, not a guess here. Left unresolved: three
    ///     <c>MeditationRegenSystemTests</c> are marked <c>Skip</c> pending that contract.
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
    ///     is enforced at all for the character's current action. Set on every accepted avatar action by
    ///     <see cref="CharacterMotionWhitelist" /> (<see cref="Zone.HandleMove" />); pre-set to true
    ///     here (enforced, ceiling zero) so a character that has never yet had an action accepted starts in the
    ///     same deny-all-attacks state the legacy session itself starts in
    ///     (Server/ts25zone/S04_MyWork02.cpp:855-869), not merely the whitelist's own per-call fallback.
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
    ///     for the character's current action. Reset to 0 by every accepted avatar action
    ///     (<see cref="Zone.HandleMove" />), incremented by <see cref="AttackPacketBudget.TryConsume" />.
    /// </summary>
    public int AttackSubPacketsUsed { get; set; }

    /// <summary>
    ///     Live BUFF_INFO mirror (35 slots x [value, duration-in-legacy-ticks]) -- fed to
    ///     <see cref="Stats.StatCalculator.ComputeEffectiveStats" /> and decremented/expired by
    ///     <see cref="Simulation.BuffExpirySystem" /> every legacy tick.
    /// </summary>
    /// <remarks>
    ///     Deliberately a fresh per-instance array (never
    ///     <c>Fenrir.Network.Serialization.Packets.Shared.WorldStateTemplates.ZeroedBuffInfo</c>,
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
