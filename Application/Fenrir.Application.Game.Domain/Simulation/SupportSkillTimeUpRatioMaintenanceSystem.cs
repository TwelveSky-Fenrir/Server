using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Once-per-real-minute aging/expiry check for the two source fields behind
///     <c>mSupportSkillTimeUpRatio</c> (behavior contract "buff-application-stacking-decay") -- the same
///     per-minute gate <see cref="PlayTimeAccrualSystem" /> uses for PlayTime1-3/Event
///     (S07_MyGame04.cpp:889 is the shared outer gate both systems' citations fall under), kept as an
///     independent system/accumulator rather than folded into that one so each keeps its own single,
///     narrowly-named responsibility.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:1027-1044 (<c>aBuffX2Time</c> decremented every minute,
///     ratio recomputed once the counter reaches zero) and :1081-1093 (<c>aPremium</c> checked against the
///     current time, cleared, ratio recomputed once found expired).
///     <para>
///         Full-catch-up (integer-division) cadence, the same choice <see cref="PlayTimeAccrualSystem" />
///         made and for the same reason: a burst of N accumulated legacy ticks (a stalled host catching up)
///         must age <see cref="PlayerRuntimeState.BuffX2Time" /> down by N real minutes' worth in one pass,
///         not just one, to match what N sequential per-minute firings would have done.
///     </para>
///     <para>
///         Deliberately NOT modeled: the citation's own five-explicitly-listed-server-number suppression of
///         the <c>aBuffX2Time</c> decrement -- Fenrir has no equivalent "legacy server number" concept to gate
///         against, and the contract itself flags this as unconfirmed (not observed whether some other
///         mechanism ages the counter on those servers instead). The decrement below runs unconditionally on
///         every shard until that is resolved. Also not modeled: the item-consumption recompute triggers
///         (a premium-granting cash item, and the "Buff Duration Pill" item 1132 increasing
///         <c>aBuffX2Time</c>) -- see <see cref="PlayerRuntimeState.BuffX2Time" />'s own remarks for why.
///     </para>
/// </remarks>
public sealed class SupportSkillTimeUpRatioMaintenanceSystem : ISimulationSystem
{
    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        foreach (var state in zone.Players)
            TickPlayer(state, legacyTicksElapsed);
    }

    private static void TickPlayer(PlayerRuntimeState state, int legacyTicksElapsed)
    {
        state.SupportSkillTimeUpRatioAccrualTicks += legacyTicksElapsed;
        var minutesElapsed =
            state.SupportSkillTimeUpRatioAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return;

        state.SupportSkillTimeUpRatioAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;

        // Edge-triggered, not level-triggered: only ages/recomputes while the counter is still positive going
        // in, matching "the multiplier is recomputed specifically in the branch taken once that counter has
        // reached zero" -- once already at 0, further elapsed minutes touch neither the field nor the ratio.
        var recomputeNeeded = false;
        if (state.BuffX2Time > 0)
        {
            state.BuffX2Time = Math.Max(0, state.BuffX2Time - minutesElapsed);
            if (state.BuffX2Time == 0)
                recomputeNeeded = true;
        }

        var nowUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (state.PremiumExpireUtc != 0 && state.PremiumExpireUtc < nowUnixSeconds)
        {
            state.PremiumExpireUtc = 0;
            recomputeNeeded = true;
        }

        if (recomputeNeeded)
            state.RecomputeSupportSkillTimeUpRatio(nowUnixSeconds);
    }
}
