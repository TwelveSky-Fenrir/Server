using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildBuffDecayTests
{
    private static readonly DateTime Now = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_Inactive_IsANoOp()
    {
        var guild = Guild(buffState: 0, buffTime: 60, buffTimeForDiff: Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(10));

        Assert.False(result.Changed);
        Assert.Equal(guild.BuffTime, result.BuffTime);
        Assert.Equal(guild.BuffTimeForDiff, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_ActiveButLessThanAMinuteElapsed_IsANoOp()
    {
        var guild = Guild(buffState: 1, buffTime: 60, buffTimeForDiff: Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddSeconds(30));

        Assert.False(result.Changed);
    }

    [Fact]
    public void Apply_ActiveWithReserveRemaining_DecrementsByWholeElapsedMinutes_AndAdvancesCheckpointByExactlyThat()
    {
        var guild = Guild(buffState: 1, buffTime: 60, buffTimeForDiff: Now.Ticks);

        // 7 minutes 40 seconds elapsed -> only 7 whole minutes consumed, the remaining 40s carries to next call.
        var later = Now.AddMinutes(7).AddSeconds(40);
        var result = GuildBuffDecay.Apply(guild, later);

        Assert.True(result.Changed);
        Assert.Equal(2, result.BuffType);
        Assert.Equal(1, result.BuffState);
        Assert.Equal(53, result.BuffTime);
        Assert.Equal(Now.AddMinutes(7).Ticks, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_ReserveExhausted_DeactivatesAndClearsType()
    {
        var guild = Guild(buffState: 1, buffTime: 5, buffTimeForDiff: Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(5));

        Assert.True(result.Changed);
        Assert.Equal(0, result.BuffType);
        Assert.Equal(0, result.BuffState);
        Assert.Equal(0, result.BuffTime);
    }

    [Fact]
    public void Apply_ReserveOverspent_ClampsToZero_RatherThanGoingNegative()
    {
        var guild = Guild(buffState: 1, buffTime: 5, buffTimeForDiff: Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(90));

        Assert.True(result.Changed);
        Assert.Equal(0, result.BuffTime);
        Assert.Equal(0, result.BuffState);
    }

    [Fact]
    public void Apply_NeverStampedCheckpoint_TreatsEpochZeroAsFullyStale_AndExpiresImmediately()
    {
        // BuffTimeForDiff defaults to 0 (game.Guilds' DF_Guilds_BuffTimeForDiff) until an activation stamps it;
        // GuildActionHandler.HandleBuffAsync fixes that going forward, but this must still degrade safely for
        // any row a decay pass reaches before its first stamped activation.
        var guild = Guild(buffState: 1, buffTime: 999, buffTimeForDiff: 0);

        var result = GuildBuffDecay.Apply(guild, Now);

        Assert.True(result.Changed);
        Assert.Equal(0, result.BuffTime);
        Assert.Equal(0, result.BuffState);
    }

    private static GuildSummaryDto Guild(int buffState, int buffTime, long buffTimeForDiff)
    {
        return new GuildSummaryDto(GuildId: 10, Name: "Aesir", Grade: 1, MasterCharacterId: 1, Points: 0,
            BuffType: 2, BuffState: buffState, BuffTime: buffTime, BuffTimeForDiff: buffTimeForDiff, Logo: 0,
            CreatedAtUtc: Now, MemberCount: 1);
    }
}
