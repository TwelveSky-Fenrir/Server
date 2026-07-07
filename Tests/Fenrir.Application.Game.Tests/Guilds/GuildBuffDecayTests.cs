using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Data.Abstractions.Guilds;

namespace Fenrir.Application.Game.Tests.Guilds;

public class GuildBuffDecayTests
{
    private static readonly DateTime Now = new(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_ReserveIsZero_IsANoOp()
    {
        // Mirrors legacy's own row-selection WHERE clause (gBuffTime != 0) -- a guild with nothing banked is
        // never selected at all, regardless of BuffState or the checkpoint.
        var guild = Guild(0, 0, Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(10));

        Assert.False(result.Changed);
        Assert.Equal(guild.BuffTime, result.BuffTime);
        Assert.Equal(guild.BuffTimeForDiff, result.BuffTimeForDiff);
    }

    [Fact]
    public void Apply_NeverActivated_ButReserveAndCheckpointBothPresent_StillDecays()
    {
        // The finding this guards: MyGame::LogicGuildBuff's row-selection query
        // (Server/ts25center/S07_MyGame01.cpp:291-331) has no gBuffState condition at all -- a guild that
        // topped up its reserve (Guild Scroll) but never called tSort=14 to choose/activate a buff type still
        // burns its reserve down in real time exactly like an already-active one, once BuffTimeForDiff is
        // non-zero. Fenrir previously required BuffState==1 before even considering decay, so this exact guild
        // (BuffState=0, reserve and checkpoint both present) never lost any time while it sat unused.
        var guild = Guild(0, 60, Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(10));

        Assert.True(result.Changed);
        Assert.Equal(50, result.BuffTime);
        Assert.Equal(guild.BuffState, result.BuffState); // decay never itself flips BuffState to "activated"
    }

    [Fact]
    public void Apply_ActiveButLessThanAMinuteElapsed_IsANoOp()
    {
        var guild = Guild(1, 60, Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddSeconds(30));

        Assert.False(result.Changed);
    }

    [Fact]
    public void Apply_ActiveWithReserveRemaining_DecrementsByWholeElapsedMinutes_AndAdvancesCheckpointByExactlyThat()
    {
        var guild = Guild(1, 60, Now.Ticks);

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
    public void Apply_ReserveExhausted_FloorsOnlyBuffTime_LeavesTypeAndStateUntouched()
    {
        // Legacy parity (Server/ts25center/S07_MyGame01.cpp:316-330): the expiry UPDATE only ever rewrites
        // gBuffTime -- gBuffType/gBuffState are never reset by this pass, so a later top-up (e.g. a Guild
        // Scroll) silently resumes the same previously-selected buff type with no reselection needed.
        // "Active" is derived at read time instead (GuildInfoProjection.Build), not stored as a flag here.
        var guild = Guild(1, 5, Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(5));

        Assert.True(result.Changed);
        Assert.Equal(guild.BuffType, result.BuffType);
        Assert.Equal(guild.BuffState, result.BuffState);
        Assert.Equal(0, result.BuffTime);
    }

    [Fact]
    public void Apply_ReserveOverspent_ClampsBuffTimeToZero_RatherThanGoingNegative_AndLeavesTypeAndStateUntouched()
    {
        var guild = Guild(1, 5, Now.Ticks);

        var result = GuildBuffDecay.Apply(guild, Now.AddMinutes(90));

        Assert.True(result.Changed);
        Assert.Equal(0, result.BuffTime);
        Assert.Equal(guild.BuffType, result.BuffType);
        Assert.Equal(guild.BuffState, result.BuffState);
    }

    [Fact]
    public void Apply_NeverStampedCheckpoint_IsExcludedEntirely_RegardlessOfBuffState()
    {
        // BuffTimeForDiff defaults to 0 (game.Guilds' DF_Guilds_BuffTimeForDiff). Legacy's own row-selection
        // query (Server/ts25center/S07_MyGame01.cpp:291-331) requires gBuffTimeForDiff != 0 to even select the
        // row -- a still-zero checkpoint means "no recompute, no write, no broadcast" for that guild this pass,
        // not "treat as fully elapsed and zero it out". What (re-)stamps this checkpoint away from zero is not
        // this behavior's concern (not observed in the cited range -- see GuildBuffDecay's own remarks).
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
