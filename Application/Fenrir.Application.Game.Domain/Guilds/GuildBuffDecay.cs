namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildBuffDecay
{
    public static Result Apply(GuildSummaryDto guild, DateTime nowUtc)
    {
        if (guild.BuffTime <= 0 || guild.BuffTimeForDiff == 0)
            return new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff);

        var remainingMinutes = (guild.BuffTimeForDiff - nowUtc.Ticks) / TimeSpan.TicksPerMinute;
        var floored = remainingMinutes < 1 ? 0 : (int)remainingMinutes;

        return floored == guild.BuffTime
            ? new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff)
            : new Result(true, guild.BuffType, guild.BuffState, floored, guild.BuffTimeForDiff);
    }

        public readonly record struct Result(bool Changed, int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);
}
