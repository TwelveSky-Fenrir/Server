using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildBuffDecayTests
{
    private static readonly DateTime Now = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_ReserveIsZero_IsANoOp()
    {
        var guild = Guild(0, 0, Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(10));

        Assert.False(result.Changed);
        Assert.Equal(guild.BuffTime, result.BuffTime);
        Assert.Equal(guild.BuffTimeForDiff, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_NeverActivated_ButReserveAndBaselineBothPresent_StillDecays()
    {
        var guild = Guild(0, 60, Now.AddMinutes(60).Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(10));

        Assert.True(result.Changed);
        Assert.Equal(50, result.BuffTime);
        Assert.Equal(guild.BuffState, result.BuffState);
        Assert.Equal(guild.BuffTimeForDiff, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_RecomputedRemainingStillEqualsStoredValue_IsANoOp()
    {
        var guild = Guild(1, 60, Now.AddMinutes(60).AddSeconds(30).Ticks);

        var result = GuildBuffDecay.Apply(guild, Now);

        Assert.False(result.Changed);
    }

    [Fact]
    public void Apply_ActiveWithReserveRemaining_RecomputesWholeMinutesRemaining_AgainstTheFixedBaseline_NeverAdvancingIt()
    {
        var guild = Guild(1, 60, Now.AddMinutes(53).AddSeconds(40).Ticks);

        var result = GuildBuffDecay.Apply(guild, Now);

        Assert.True(result.Changed);
        Assert.Equal(2, result.BuffType);
        Assert.Equal(1, result.BuffState);
        Assert.Equal(53, result.BuffTime);
        Assert.Equal(guild.BuffTimeForDiff, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_ReserveExhausted_FloorsOnlyBuffTime_LeavesTypeAndStateAndBaselineUntouched()
    {
        var guild = Guild(1, 5, Now.AddMinutes(5).Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(5));

        Assert.True(result.Changed);
        Assert.Equal(guild.BuffType, result.BuffType);
        Assert.Equal(guild.BuffState, result.BuffState);
        Assert.Equal(0, result.BuffTime);
        Assert.Equal(guild.BuffTimeForDiff, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_ReserveOverspent_ClampsBuffTimeToZero_RatherThanGoingNegative_AndLeavesTypeAndStateUntouched()
    {
        var guild = Guild(1, 5, Now.AddMinutes(-85).Ticks);

        var result = GuildBuffDecay.Apply(guild, Now);

        Assert.True(result.Changed);
        Assert.Equal(0, result.BuffTime);
        Assert.Equal(guild.BuffType, result.BuffType);
        Assert.Equal(guild.BuffState, result.BuffState);
    }

    [Fact]
    public void Apply_NeverStampedBaseline_IsExcludedEntirely_RegardlessOfBuffState()
    {
        var guild = Guild(1, 999, 0);

        var result = GuildBuffDecay.Apply(guild, Now);

        Assert.False(result.Changed);
        Assert.Equal(guild.BuffTime, result.BuffTime);
        Assert.Equal(guild.BuffType, result.BuffType);
        Assert.Equal(guild.BuffState, result.BuffState);
    }

    private static GuildSummaryDto Guild(int buffState, int buffTime, long buffTimeForDiff)
    {
        return new GuildSummaryDto(10, "Aesir", 1, 1, 0,
            2, buffState, buffTime, buffTimeForDiff, 0,
            Now, 1);
    }
}
