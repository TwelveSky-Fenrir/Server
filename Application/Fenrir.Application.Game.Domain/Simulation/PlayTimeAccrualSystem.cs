using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Per-real-minute play-time accrual (<c>S07_MyGame04.cpp:887-911</c>): every 120 legacy ticks (60 s at
///     TimeLogic=500 ms), increments <see cref="PlayerRuntimeState.PlayTime1" />/
///     <see cref="PlayerRuntimeState.PlayTime2" />/<see cref="PlayerRuntimeState.PlayTime3" />/
///     <see cref="PlayerRuntimeState.PlayTimeEvent" /> together and broadcasts the new PlayTime2 to the
///     owning client only, via <c>AvatarStatUpdateResponse</c> (Sort=16, S016PLAYTIME2) -- unconditional for
///     every active in-world player, no build macro gates it.
/// </summary>
/// <remarks>
///     <para>
///         Full-catch-up (integer-division) cadence, the same choice <see cref="StunCountdownSystem" /> made
///         and for the same reason given there: the legacy's own gate is a strict tick-count equality check
///         that fires once per elapsed real minute even across a stalled host, so a burst of N accumulated
///         legacy ticks must accrue and broadcast N minutes' worth, not just one -- unlike
///         <see cref="PetActivitySystem" />'s single-decrement-per-call choice for its own less time-critical
///         decay.
///     </para>
///     <para>
///         Deliberately does NOT call <see cref="PlayerRuntimeState.MarkProgressDirty" />: none of the four
///         counters this system touches has a persisted-column/write-behind path yet (see
///         <see cref="PlayerRuntimeState.PlayTime1" />'s own remarks for why this pass leaves that unwired),
///         so marking every online character's progression dirty every single real minute -- for fields the
///         existing progress-flush TVP would not even write -- would only add a steady-state DB flush cost
///         with zero benefit. <see cref="Zone.GrantWarPoints" />/<see cref="Zone.GrantBloodPoints" /> mark
///         dirty despite the same "not yet persisted" posture because those are rare, event-driven grants (a
///         kill), not a guaranteed once-a-minute tick for every single online player -- a materially
///         different frequency/cost tradeoff.
///     </para>
///     <para>
///         The daily-mission play-time sub-increment the legacy fires in this same cited block (guarded by a
///         macro confirmed absent -- via a targeted grep across the whole source tree -- from every shipped
///         build configuration: <c>S07_MyGame04.cpp:904</c>, <c>S07_MyGame02.cpp:2780</c>,
///         <c>S04_MyWork02.cpp:14554,14583</c>) is dead code and deliberately not ported here. The
///         immediately-following server-number/zone-type exclusion block (<c>S07_MyGame04.cpp:913-928</c>) is
///         outside this system's asserted scope -- flagged (per the source behavior contract) for a
///         <c>cpp-ts25-explorer</c> re-check before assuming every server/zone in the cluster ticks this
///         unconditionally.
///     </para>
/// </remarks>
public sealed class PlayTimeAccrualSystem : ISimulationSystem
{
    /// <summary>AvatarStatUpdateResponse.Sort for S016PLAYTIME2 (Server/Header/Protocol/STRUCT.h:1531).</summary>
    private const int PlayTime2StatSort = 16;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
        {
            state.PlayTimeAccrualTicks += legacyTicksElapsed;
            var minutesElapsed = state.PlayTimeAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
            if (minutesElapsed <= 0)
                continue;

            state.PlayTimeAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;
            state.PlayTime1 += minutesElapsed;
            state.PlayTime2 += minutesElapsed;
            state.PlayTime3 += minutesElapsed;
            state.PlayTimeEvent += minutesElapsed;

            state.Session.Send(new AvatarStatUpdateResponse
                { Sort = PlayTime2StatSort, Value = state.PlayTime2, Value2 = 0 });
        }
    }
}
