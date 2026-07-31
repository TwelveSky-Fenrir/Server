namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildBuffDecay
{
    // BuffTimeForDiff est un time_t legacy : des SECONDES epoch, jamais des ticks .NET
    // (Server/ts25extra/S08_MyDB.cpp:1169-1174 borne a now_t puis ajoute minutes*60 ;
    // Server/Header/CSQLGuild.cpp:226 relit par difftime). A l'expiration le legacy remet UNIQUEMENT
    // gBuffTime a zero et conserve gBuffType/gBuffState (Server/ts25center/S07_MyGame01.cpp:318-329).
    public static Result Apply(GuildSummaryDto guild, DateTimeOffset nowUtc)
    {
        if (guild.BuffTime <= 0 || guild.BuffTimeForDiff == 0)
            return new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff);

        var remainingMinutes = (guild.BuffTimeForDiff - nowUtc.ToUnixTimeSeconds()) / 60;
        var floored = remainingMinutes < 1 ? 0 : (int)remainingMinutes;

        return floored == guild.BuffTime
            ? new Result(false, guild.BuffType, guild.BuffState, guild.BuffTime, guild.BuffTimeForDiff)
            : new Result(true, guild.BuffType, guild.BuffState, floored, guild.BuffTimeForDiff);
    }

    public readonly record struct Result(bool Changed, int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);
}
