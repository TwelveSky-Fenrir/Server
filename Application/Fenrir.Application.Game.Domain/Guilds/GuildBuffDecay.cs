namespace Fenrir.Application.Game.Domain.Guilds;

/// <summary>
///     Pure decay step for one guild's active buff reserve (game.Guilds BuffType/BuffState/BuffTime/BuffTimeForDiff).
///     GUILD_WORK tSort 14 (<see cref="Handlers.Guilds.GuildActionHandler" />'s SetGuildBuffAsync) only ever
///     chooses/switches a buff type and never itself touches BuffTime or its checkpoint (matches legacy's
///     UpdateGuildBuffType, Server/ts25extra/S08_MyDB.cpp:1188-1191); nothing previously counted the reserve
///     down over real time, so an activated buff was immortal until <see cref="GuildBuffDecayHost" /> started
///     calling this every pass.
/// </summary>
/// <remarks>
///     Eligibility mirrors <c>MyGame::LogicGuildBuff</c>'s own row-selection query verbatim
///     (<c>Server/ts25center/S07_MyGame01.cpp:291-331</c>: <c>WHERE gBuffTime != 0 AND gBuffTimeForDiff != 0</c>)
///     -- there is no BuffState condition anywhere in that query or the loop that follows it, so a guild
///     decays purely from these two stored numeric fields, independent of whether a buff type has ever been
///     chosen/activated for it. A guild that topped up its reserve (Guild Scroll) but never activated a type
///     still burns the reserve down once BuffTimeForDiff is non-zero, exactly like an already-active one; this
///     behavior never itself stamps BuffTimeForDiff away from zero (see the cited range's own ambiguity about
///     what stamps it, not resolved here).
///     BuffTimeForDiff is a UTC tick checkpoint, not a duration. Only whole elapsed minutes are consumed per
///     call and the checkpoint advances by exactly that many minutes (never snapped to "now") so a sub-minute
///     remainder is never silently dropped between calls.
/// </remarks>
/// <remarks>
///     Legacy parity for reserve exhaustion (<c>Server/ts25center/S07_MyGame01.cpp:316-330</c>): the maintenance
///     pass's expiry UPDATE only ever rewrites <c>gBuffTime</c> -- it never clears <c>gBuffType</c> or
///     <c>gBuffState</c>, anywhere. Whether the buff currently applies is instead derived fresh wherever it's
///     read (e.g. <c>Server/ts25login/S08_MyDB.cpp:661</c>: <c>gBuffTime &gt; 0 &amp;&amp; gBuffState &gt; 0</c>),
///     never stored as a pre-computed flag. Zeroing BuffType/BuffState here would mean a Guild Scroll top-up
///     after exhaustion could no longer silently resume the guild's previously-selected buff the way legacy
///     does -- so on exhaustion only BuffTime is floored to zero; BuffType and BuffState pass through
///     unchanged. Any caller that surfaces "is the buff active" to a client must derive it the same way
///     (see <see cref="GuildInfoProjection.Build" />), not read BuffState as a ready-made flag.
/// </remarks>
public static class GuildBuffDecay
{
    public static Result Apply(GuildSummaryDto guild, DateTime nowUtc)
    {
        if (guild.BuffTime <= 0 || guild.BuffTimeForDiff == 0)
            return new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff);

        var elapsedMinutes = (nowUtc.Ticks - guild.BuffTimeForDiff) / TimeSpan.TicksPerMinute;
        if (elapsedMinutes <= 0)
            return new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff);

        var newForDiff = guild.BuffTimeForDiff + elapsedMinutes * TimeSpan.TicksPerMinute;
        var remaining = guild.BuffTime - elapsedMinutes;

        return remaining > 0
            // Still counting down: BuffState/BuffType pass through untouched -- decay never itself flips a
            // guild into "activated" (BuffState=1) just because its reserve happens to be non-zero.
            ? new Result(true, guild.BuffType, guild.BuffState, (int)remaining, newForDiff)
            // Reserve exhausted: legacy only floors BuffTime to zero here -- BuffType/BuffState are never
            // reset (see remarks above), so pass them through untouched instead of clearing them.
            : new Result(true, guild.BuffType, guild.BuffState, 0, newForDiff);
    }

    /// <param name="Changed">False means every other field is an untouched copy of the input -- nothing to persist.</param>
    public readonly record struct Result(bool Changed, int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);
}
