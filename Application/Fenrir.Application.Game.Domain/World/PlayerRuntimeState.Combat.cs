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
}
