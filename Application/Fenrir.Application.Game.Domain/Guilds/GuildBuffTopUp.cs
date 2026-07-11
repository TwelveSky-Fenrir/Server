namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildBuffTopUp
{
    public static Result Apply(GuildSummaryDto guild, int minutes, DateTime nowUtc)
    {
        var baseline = guild.BuffTimeForDiff < nowUtc.Ticks ? nowUtc.Ticks : guild.BuffTimeForDiff;
        var newBaseline = baseline + minutes * TimeSpan.TicksPerMinute;

        var remainingMinutes = (newBaseline - nowUtc.Ticks) / TimeSpan.TicksPerMinute;
        var flooredBuffTime = remainingMinutes < 0 ? 0 : (int)remainingMinutes;

        return new Result(guild.BuffType, guild.BuffState, flooredBuffTime, newBaseline);
    }

    public readonly record struct Result(int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);
}
