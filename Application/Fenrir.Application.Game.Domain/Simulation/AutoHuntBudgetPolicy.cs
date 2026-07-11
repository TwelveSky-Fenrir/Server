namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Pure policy for the two-tier paid auto-hunt-time budget decrement
///     (<c>Server/ts25zone/S07_MyGame04.cpp:787-824</c>, compiled live in the shipped build under
///     <c>#ifndef FREE_HUNT</c> -- <c>FREE_HUNT</c> sits inside the dead <c>#else</c> of <c>#ifdef M33</c>, so
///     the budget block IS active in production). No I/O, no Zone/PlayerRuntimeState/WorldDataCache dependency --
///     the caller (<see cref="AutoHuntTickSystem" />) owns reading/writing the per-character tiers and sending
///     the client notifications.
/// </summary>
/// <remarks>
///     Two-tier precedence (<c>S07_MyGame04.cpp:788-824</c>): the day-tier is a calendar-expiry timestamp
///     (legacy raw <c>YYYYMMDD</c>, <see cref="GameDate" />) that, once the current date passes it, is zeroed;
///     only once the day-tier is exhausted does the minute-tier -- a consumable count of minutes -- decrement by
///     one per elapsed real-world minute (floored at zero). When BOTH tiers reach zero the character is relocated
///     back to the designated auto zone (<c>B_RETURN_TO_AUTO_ZONE</c>, <c>S05_MyTransfer.cpp:1166-1179</c>) and
///     that tick's remaining auto-hunt processing is abandoned.
///     <para>
///         <b>Deliberate Fenrir divergence (present-based engagement gate):</b> legacy's block runs on every
///         auto-hunting player every tick and relocates outright whenever both tiers are zero -- i.e. auto-hunt
///         is a paid feature that bounces anyone with no purchased time. Fenrir has no paid-time acquisition path
///         yet (the cash-shop/subscription grant family is unimplemented, so both tiers are always <c>0</c>, the
///         same "real but currently unreachable" posture as <c>PlayerRuntimeState.AutoBuffTime</c>/<c>AnimalTime</c>);
///         reproducing the literal <c>0/0</c>-relocate would immediately bounce every player who enables the
///         already-shipped auto-hunt buff loop, a self-inflicted denial of service exactly like the empty
///         GM-allowlist lockout this project already learned from. So this policy engages only for a player who
///         currently holds a positive budget in either tier: a <c>0/0</c> player is a clean no-op
///         (<see cref="Signal.None" />), never <see cref="Signal.Exhausted" />. The trigger to restore literal
///         legacy fidelity is explicit: once a paid-time grant path lands, gate unconditional enforcement behind
///         an operator config flag (see this workstream's open questions) rather than silently on <c>0/0</c>.
///     </para>
/// </remarks>
public static class AutoHuntBudgetPolicy
{
    public enum Signal
    {
        /// <summary>No change this tick (including the present-based no-op for a <c>0/0</c> player).</summary>
        None,

        /// <summary>The day-tier's calendar expiry passed and it was zeroed; the minute-tier still has time left.</summary>
        DayBudgetExpired,

        /// <summary>The minute-tier decremented by one or more whole minutes this tick and still has time left.</summary>
        MinuteBudgetDecremented,

        /// <summary>
        ///     Both tiers are now zero -- the caller relocates the character to the auto zone and abandons the rest of this
        ///     tick.
        /// </summary>
        Exhausted
    }

    /// <param name="dayBudget"><c>PlayerRuntimeState.AutoHuntPaidDayBudget</c> (raw <c>YYYYMMDD</c>; 0 = none).</param>
    /// <param name="minuteBudget"><c>PlayerRuntimeState.AutoHuntPaidMinuteBudget</c> (remaining minutes; 0 = none).</param>
    /// <param name="minuteAccrualTicks"><c>PlayerRuntimeState.AutoHuntBudgetMinuteAccrualTicks</c>.</param>
    /// <param name="legacyTicksElapsed">Whole legacy ticks elapsed this pass (full-catch-up, floored per minute).</param>
    /// <param name="todayDate">The current date in the raw <c>YYYYMMDD</c> encoding (<see cref="GameDate.Today" />).</param>
    public static Result Advance(int dayBudget, int minuteBudget, int minuteAccrualTicks, int legacyTicksElapsed,
        int todayDate)
    {
        // Present-based engagement gate -- see this type's own remarks for the deliberate divergence.
        if (dayBudget <= 0 && minuteBudget <= 0)
            return new Result(0, 0, minuteAccrualTicks, Signal.None);

        // Day-tier takes precedence (S07_MyGame04.cpp:790-797): a present calendar-expiry timestamp.
        if (dayBudget > 0)
        {
            if (todayDate <= dayBudget)
                return new Result(dayBudget, minuteBudget, minuteAccrualTicks, Signal.None);

            // Day-tier just expired -> zero it. If the minute-tier is also empty, both tiers are now zero.
            return minuteBudget > 0
                ? new Result(0, minuteBudget, minuteAccrualTicks, Signal.DayBudgetExpired)
                : new Result(0, 0, minuteAccrualTicks, Signal.Exhausted);
        }

        // Minute-tier (day-tier already exhausted, minute-tier present): once-per-real-minute decrement
        // (S07_MyGame04.cpp:798-810). Full-catch-up (integer-division) cadence, matching PlayTimeAccrualSystem.
        minuteAccrualTicks += legacyTicksElapsed;
        var minutesElapsed = minuteAccrualTicks / SimulationClock.PlayTimeAccrualLegacyTicks;
        if (minutesElapsed <= 0)
            return new Result(0, minuteBudget, minuteAccrualTicks, Signal.None);

        minuteAccrualTicks -= minutesElapsed * SimulationClock.PlayTimeAccrualLegacyTicks;
        minuteBudget = Math.Max(0, minuteBudget - minutesElapsed);

        return minuteBudget > 0
            ? new Result(0, minuteBudget, minuteAccrualTicks, Signal.MinuteBudgetDecremented)
            : new Result(0, 0, minuteAccrualTicks, Signal.Exhausted);
    }

    /// <summary>The tiers/accumulator to write back, plus what happened this tick.</summary>
    public readonly record struct Result(int DayBudget, int MinuteBudget, int MinuteAccrualTicks, Signal Signal);
}
