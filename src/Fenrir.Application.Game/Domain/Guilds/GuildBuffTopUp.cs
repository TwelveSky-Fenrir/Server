namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildBuffTopUp
{
    // Meme unite que GuildBuffDecay : BuffTimeForDiff est en SECONDES epoch. Le legacy borne d'abord la date
    // d'expiration a maintenant si elle est deja passee, puis ajoute les minutes achetees
    // (Server/ts25extra/S08_MyDB.cpp:1169-1183).
    public static Result Apply(GuildSummaryDto guild, int minutes, DateTimeOffset nowUtc)
    {
        var nowEpochSeconds = nowUtc.ToUnixTimeSeconds();
        var baseline = guild.BuffTimeForDiff < nowEpochSeconds ? nowEpochSeconds : guild.BuffTimeForDiff;
        var newBaseline = baseline + minutes * 60L;

        var remainingMinutes = (newBaseline - nowEpochSeconds) / 60;
        var flooredBuffTime = remainingMinutes < 0 ? 0 : (int)remainingMinutes;

        return new Result(guild.BuffType, guild.BuffState, flooredBuffTime, newBaseline);
    }

    public readonly record struct Result(int BuffType, int BuffState, int BuffTime, long BuffTimeForDiff);
}
