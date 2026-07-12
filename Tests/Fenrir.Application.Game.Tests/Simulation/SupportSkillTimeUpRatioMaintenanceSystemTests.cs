using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

public class SupportSkillTimeUpRatioMaintenanceSystemTests
{
    private const short PlainZone = 100;

    private static readonly TimeSpan OneMinute =
        SimulationClock.ToTimeSpan(SimulationClock.PlayTimeAccrualLegacyTicks);

    private static (Zone Zone, PlayerRuntimeState State) EnterPlayer(long premiumExpireUtc = 0,
        int buffX2Time = 0)
    {
        var zone = ZoneTestKit.CreateZone(PlainZone,
            simulationSystems: [new SupportSkillTimeUpRatioMaintenanceSystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = ZoneTestKit.EnterData(session, PlainZone) with
        {
            PremiumExpireUtc = premiumExpireUtc,
            BuffX2Time = buffX2Time
        };
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }

    [Fact]
    public void NeitherSourceActive_RatioIsOne()
    {
        var (_, state) = EnterPlayer();

        Assert.Equal(1, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void OnlyBuffDurationPillTimeActive_RatioIsTwo()
    {
        var (_, state) = EnterPlayer(buffX2Time: 30);

        Assert.Equal(2, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void OnlyPremiumActive_RatioIsTwo()
    {
        var (_, state) = EnterPlayer(premiumExpireUtc: DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds());

        Assert.Equal(2, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void BothSourcesActive_RatioIsFour()
    {
        var (_, state) = EnterPlayer(
            premiumExpireUtc: DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds(),
            buffX2Time: 30);

        Assert.Equal(4, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void PremiumFieldAlreadyPastButNotYetSweptByTick_StillCountsAsActiveOnRecompute()
    {
        var expiredButUnswept = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();

        var (_, state) = EnterPlayer(premiumExpireUtc: expiredButUnswept);

        Assert.Equal(2, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void BuffDurationPillTimeReachingZeroOnTick_RatioDropsBackToOne()
    {
        var (zone, state) = EnterPlayer(buffX2Time: 1);
        Assert.Equal(2, state.SupportSkillTimeUpRatio);

        zone.Tick(OneMinute);

        Assert.Equal(0, state.BuffX2Time);
        Assert.Equal(1, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void PremiumExpiringDuringTick_FieldIsZeroedAndRatioDropsBackToOne()
    {
        var (zone, state) = EnterPlayer(premiumExpireUtc: DateTimeOffset.UtcNow.AddSeconds(-5).ToUnixTimeSeconds());
        Assert.Equal(2, state.SupportSkillTimeUpRatio);

        zone.Tick(OneMinute);

        Assert.Equal(0, state.PremiumExpireUtc);
        Assert.Equal(1, state.SupportSkillTimeUpRatio);
    }

    [Fact]
    public void PremiumNotYetExpired_SurvivesTickUntouched()
    {
        var farFuture = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();
        var (zone, state) = EnterPlayer(premiumExpireUtc: farFuture);

        zone.Tick(OneMinute);

        Assert.Equal(farFuture, state.PremiumExpireUtc);
        Assert.Equal(2, state.SupportSkillTimeUpRatio);
    }
}
