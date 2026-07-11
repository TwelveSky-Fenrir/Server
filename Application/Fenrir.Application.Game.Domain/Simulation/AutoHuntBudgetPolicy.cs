namespace Fenrir.Application.Game.Domain.Simulation;

public static class AutoHuntBudgetPolicy
{
    public enum Signal
    {

                None,

                DayBudgetExpired,

                MinuteBudgetDecremented,

                Exhausted
    }

        public static Result Advance(int dayBudget, int minuteBudget, int minuteAccrualTicks, int legacyTicksElapsed,
        int todayDate)
    {
        if (dayBudget <= 0 && minuteBudget <= 0)
            return new Result(0, 0, minuteAccrualTicks, Signal.None);

        if (dayBudget > 0)
        {
            if (todayDate <= dayBudget)
                return new Result(dayBudget, minuteBudget, minuteAccrualTicks, Signal.None);

            return minuteBudget > 0
                ? new Result(0, minuteBudget, minuteAccrualTicks, Signal.DayBudgetExpired)
                : new Result(0, 0, minuteAccrualTicks, Signal.Exhausted);
        }

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

        public readonly record struct Result(int DayBudget, int MinuteBudget, int MinuteAccrualTicks, Signal Signal);
}
