using Fenrir.Data.Guilds;

namespace Fenrir.Application.Game.Guilds;

/// <summary>
///     Pure decay step for one guild's active buff reserve (game.Guilds BuffType/BuffState/BuffTime/BuffTimeForDiff).
///     GUILD_WORK tSort 14 (<see cref="Handlers.Guilds.GuildActionHandler" />'s HandleBuffAsync) only ever
///     activates a buff and burns down its BuffTime reserve on request; nothing previously counted that
///     reserve down over real time, so an activated buff was immortal until <see cref="GuildBuffDecayHost" />
///     started calling this every pass.
/// </summary>
/// <remarks>
///     BuffTimeForDiff is a UTC tick checkpoint (stamped fresh by HandleBuffAsync's 0-&gt;1 activation), not a
///     duration. Only whole elapsed minutes are consumed per call and the checkpoint advances by exactly that
///     many minutes (never snapped to "now") so a sub-minute remainder is never silently dropped between calls.
/// </remarks>
public static class GuildBuffDecay
{
    /// <param name="Changed">False means every other field is an untouched copy of the input -- nothing to persist.</param>
    public readonly record struct Result(bool Changed, int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);

    public static Result Apply(GuildSummaryDto guild, DateTime nowUtc)
    {
        if (guild.BuffState != 1 || guild.BuffTime <= 0)
            return new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff);

        var elapsedMinutes = (nowUtc.Ticks - guild.BuffTimeForDiff) / TimeSpan.TicksPerMinute;
        if (elapsedMinutes <= 0)
            return new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff);

        var newForDiff = guild.BuffTimeForDiff + elapsedMinutes * TimeSpan.TicksPerMinute;
        var remaining = guild.BuffTime - elapsedMinutes;

        return remaining > 0
            ? new Result(true, guild.BuffType, 1, (int)remaining, newForDiff)
            : new Result(true, 0, 0, 0, newForDiff); // reserve exhausted: deactivate and clear the chosen type too
    }
}
