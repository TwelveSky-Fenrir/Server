using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

/// <summary>
///     C6-center-audit: legacy parity for <c>MyDB::UpdateGuildBuffTime</c>
///     (Server/ts25extra/S08_MyDB.cpp:1151-1186), the missing producer half of the guild-buff mechanism --
///     see <see cref="GuildBuffTopUp" />'s own remarks for the full cited behavior and its companion
///     <see cref="GuildBuffDecay" /> decay-side half.
/// </summary>
public class GuildBuffTopUpTests
{
    private static readonly DateTime Now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_BaselineAtSchemaDefaultZero_RestartsFromNow()
    {
        // game.Guilds.BuffTimeForDiff defaults to 0 (DF_Guilds_BuffTimeForDiff) -- a guild that has never
        // been topped up before must start counting from now, not from year-1 epoch zero.
        var guild = Guild(2, 0, 0, 0L);

        var result = GuildBuffTopUp.Apply(guild, 30, Now);

        Assert.Equal(30, result.BuffTime);
        Assert.Equal(Now.AddMinutes(30).Ticks, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_BaselineAlreadyInThePast_RestartsFromNow_DoesNotStackOntoStaleTimestamp()
    {
        var staleBaseline = Now.AddMinutes(-100).Ticks;
        var guild = Guild(2, 0, 0, staleBaseline);

        var result = GuildBuffTopUp.Apply(guild, 60, Now);

        Assert.Equal(60, result.BuffTime);
        Assert.Equal(Now.AddMinutes(60).Ticks, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_BaselineStillInTheFuture_ExtendsTheExistingBaseline_RatherThanResettingTheClock()
    {
        var futureBaseline = Now.AddMinutes(10).Ticks;
        var guild = Guild(2, 1, 10, futureBaseline);

        var result = GuildBuffTopUp.Apply(guild, 30, Now);

        Assert.Equal(Now.AddMinutes(40).Ticks, result.BuffTimeForDiff);
        Assert.Equal(40, result.BuffTime);
    }

    [Fact]
    public void Apply_NeverTouchesBuffTypeOrBuffState()
    {
        var guild = Guild(3, 1, 0, 0L);

        var result = GuildBuffTopUp.Apply(guild, 30, Now);

        Assert.Equal(guild.BuffType, result.BuffType);
        Assert.Equal(guild.BuffState, result.BuffState);
    }

    [Fact]
    public void Apply_BaselineExactlyNow_IsNotTreatedAsPast_ExtendsFromThatSameInstant()
    {
        var guild = Guild(2, 0, 0, Now.Ticks);

        var result = GuildBuffTopUp.Apply(guild, 15, Now);

        Assert.Equal(Now.AddMinutes(15).Ticks, result.BuffTimeForDiff);
        Assert.Equal(15, result.BuffTime);
    }

    private static GuildSummaryDto Guild(int buffType, int buffState, int buffTime, long buffTimeForDiff)
    {
        return new GuildSummaryDto(10, "Aesir", 1, 1, 0,
            buffType, buffState, buffTime, buffTimeForDiff, 0,
            Now, 1);
    }
}
