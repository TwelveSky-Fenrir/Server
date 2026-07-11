using Fenrir.Application.Game.Domain.AntiCheat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.AntiCheat;

public class PlayerAntiCheatResetTests
{
    private static PlayerRuntimeState NewState(string? sourceIp = null)
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 1,
            Gender = 0,
            HeadType = 2,
            FaceType = 3,
            Level = 42,
            SourceIp = sourceIp
        };
    }

    [Fact]
    public void Reset_ZeroesEveryIntegerAndFlagCounter()
    {
        var state = NewState();

        state.DarkAttackUseTick = 11;
        state.DarkAttackActiveTick = 12;
        state.DarkAttackKind = 13;
        state.HitRateTick = 14;
        state.HitRateKind = 15;
        state.DodgeRateTick = 16;
        state.DodgeRateKind = 17;
        state.LastUsedSkillIndex = 18;
        state.LastUsedSkillTick = 19;
        state.LastUsedSkillWarning = 20;
        state.LastUsedSkillTotalWarning = 21;
        state.SameSpotCount = 22;
        state.BottleTick = 23;
        state.AntiAhsHackFlag = true;
        state.AutoCheckState = 24;
        state.AutoCheckAnswer1 = 25;
        state.AutoCheckAnswer2 = 26;
        state.AutoCheckTime = 27;
        state.AttackHitTime1 = 28;
        state.AttackHitTime2 = 29;

        state.ResetVolatileAntiCheatCountersOnEntry(TimeSpan.FromMinutes(5));

        Assert.Equal(0, state.DarkAttackUseTick);
        Assert.Equal(0, state.DarkAttackActiveTick);
        Assert.Equal(0, state.DarkAttackKind);
        Assert.Equal(0, state.HitRateTick);
        Assert.Equal(0, state.HitRateKind);
        Assert.Equal(0, state.DodgeRateTick);
        Assert.Equal(0, state.DodgeRateKind);
        Assert.Equal(0, state.LastUsedSkillIndex);
        Assert.Equal(0, state.LastUsedSkillTick);
        Assert.Equal(0, state.LastUsedSkillWarning);
        Assert.Equal(0, state.LastUsedSkillTotalWarning);
        Assert.Equal(0, state.SameSpotCount);
        Assert.Equal(0, state.BottleTick);
        Assert.False(state.AntiAhsHackFlag);
        Assert.Equal(0, state.AutoCheckState);
        Assert.Equal(0, state.AutoCheckAnswer1);
        Assert.Equal(0, state.AutoCheckAnswer2);
        Assert.Equal(0, state.AutoCheckTime);
        Assert.Equal(0, state.AttackHitTime1);
        Assert.Equal(0, state.AttackHitTime2);
    }

    [Fact]
    public void Reset_BaselinesEveryTimerToTheZoneClock()
    {
        var state = NewState();
        var now = TimeSpan.FromMinutes(7);

        state.ResetVolatileAntiCheatCountersOnEntry(now);

        Assert.Equal(now, state.CpExchangeTick);
        Assert.Equal(now, state.CpRfcTick);
        foreach (var cadence in Enum.GetValues<AntiCheatCadence>())
            Assert.Equal(now, state.Cadences.Baseline(cadence));
    }

    [Fact]
    public void SourceIp_IsIdentity_NotTouchedByReset()
    {
        var state = NewState("203.0.113.7");
        state.ResetVolatileAntiCheatCountersOnEntry(TimeSpan.FromMinutes(1));
        Assert.Equal("203.0.113.7", state.SourceIp);
    }
}
