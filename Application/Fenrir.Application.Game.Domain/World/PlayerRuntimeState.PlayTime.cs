namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     aPlayTime1 -- incremented in lockstep with <see cref="PlayTime2" />/<see cref="PlayTime3" />/
    ///     <see cref="PlayTimeEvent" /> by <see cref="Simulation.PlayTimeAccrualSystem" />'s per-real-minute
    ///     tick. No confirmed read site anywhere in the legacy source beyond the shared increment and the raw
    ///     persisted-column/field-mapping declarations (Server/Header/Protocol/STRUCT.h:328,
    ///     Server/Header/CSQLAvatar.cpp:560) -- deliberately session-only here (not yet persisted), same
    ///     "no reviewed citation justifies the added write-behind surface" posture as
    ///     <see cref="TowerCpMilestoneCounter" />'s own remarks: resets to 0 on every world (re)entry.
    /// </summary>
    public int PlayTime1 { get; set; }

    /// <summary>
    ///     aPlayTime2 -- the "minutes played this session" HUD stat, broadcast to the owning client only via
    ///     <c>AvatarStatUpdateResponse</c> (Sort=16, S016PLAYTIME2) every real minute by
    ///     <see cref="Simulation.PlayTimeAccrualSystem" />. Same session-only posture as <see cref="PlayTime1" />
    ///     -- resets to 0 on relog rather than accumulating across sessions.
    /// </summary>
    public int PlayTime2 { get; set; }

    /// <summary>aPlayTime3 -- see <see cref="PlayTime1" />'s own remarks; same lockstep-only, no other confirmed read site.</summary>
    public int PlayTime3 { get; set; }

    /// <summary>
    ///     aPlayTimeEvent -- accrues in lockstep with <see cref="PlayTime1" />-<see cref="PlayTime3" /> but is
    ///     the only one of the four ever consumed: reset to 0 by TimeExchange
    ///     (<c>IGenericActionService.TimeExchangeAsync</c>, tSort 237), which converts every accrued minute
    ///     into 694 teacher points + 400 pet experience. Same session-only posture as <see cref="PlayTime1" />
    ///     -- an unclaimed accrual is lost on relog. This is a documented, not silent, gap: see
    ///     <see cref="Simulation.PlayTimeAccrualSystem" />'s own remarks for why this pass deliberately doesn't
    ///     add DB persistence for the whole family.
    /// </summary>
    public int PlayTimeEvent { get; set; }

    /// <summary>
    ///     Legacy-tick accumulator for <see cref="Simulation.PlayTimeAccrualSystem" />'s own 60s (120-legacy-tick)
    ///     cadence -- never read by anything else, same role as <see cref="PetActivityDecayTicks" />.
    /// </summary>
    public int PlayTimeAccrualTicks { get; set; }
}
