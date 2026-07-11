using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

public class TimedBuffCountdownSystemTests
{
    private static readonly TimeSpan OneMinute =
        SimulationClock.ToTimeSpan(SimulationClock.PlayTimeAccrualLegacyTicks);

    private const short PlainZone = 100;

    private const short WarZone38 = 38;

    private static (Zone Zone, PlayerRuntimeState State) EnterPlayer(short mapId)
    {
        var zone = ZoneTestKit.CreateZone(mapId, simulationSystems: [new TimedBuffCountdownSystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }


    [Fact]
    public void GroupATimers_OnPlainZone_DecrementByOnePerMinute()
    {
        var (zone, state) = EnterPlayer(PlainZone);
        state.DropItemTime = 5;
        state.FightingGodForDestroy = 3;
        state.DoubleExpTime1 = 2;
        state.DoubleExpTime2 = 1;

        zone.Tick(OneMinute);

        Assert.Equal(4, state.DropItemTime);
        Assert.Equal(2, state.FightingGodForDestroy);
        Assert.Equal(1, state.DoubleExpTime1);
        Assert.Equal(0, state.DoubleExpTime2);
    }

    [Fact]
    public void GroupATimers_FrozenOnGroupBExcludedServer()
    {
        var (zone, state) = EnterPlayer(WarZone38);
        state.DropItemTime = 5;
        state.DoubleExpTime1 = 5;

        zone.Tick(OneMinute);

        Assert.Equal(5, state.DropItemTime);
        Assert.Equal(5, state.DoubleExpTime1);
    }

    [Fact]
    public void GroupATimer_AlreadyZero_IsLeftUntouched()
    {
        var (zone, state) = EnterPlayer(PlainZone);
        state.DropItemTime = 0;

        zone.Tick(OneMinute);

        Assert.Equal(0, state.DropItemTime);
    }

    [Fact]
    public void BelowOneMinute_NothingDecrements()
    {
        var (zone, state) = EnterPlayer(PlainZone);
        state.DropItemTime = 5;

        zone.Tick(SimulationClock.ToTimeSpan(60));

        Assert.Equal(5, state.DropItemTime);
    }

    [Fact]
    public void StalledHost_CatchesUpTheWholeMinutesElapsedInOnePass()
    {
        var (zone, state) = EnterPlayer(PlainZone);
        state.DropItemTime = 5;

        zone.Tick(SimulationClock.ToTimeSpan(3 * SimulationClock.PlayTimeAccrualLegacyTicks));

        Assert.Equal(2, state.DropItemTime);
    }


    [Fact]
    public void GroupBTimers_OnWarZone_DecrementByOnePerMinute()
    {
        var (zone, state) = EnterPlayer(WarZone38);
        state.DmgBoost = 5;
        state.HPBoost = 4;
        state.CriBoost = 3;
        state.WarriorPill = 2;
        state.WarriorScroll = 1;

        zone.Tick(OneMinute);

        Assert.Equal(4, state.DmgBoost);
        Assert.Equal(3, state.HPBoost);
        Assert.Equal(2, state.CriBoost);
        Assert.Equal(1, state.WarriorPill);
        Assert.Equal(0, state.WarriorScroll);
    }

    [Fact]
    public void GroupBTimers_FrozenOnPlainZone()
    {
        var (zone, state) = EnterPlayer(PlainZone);
        state.DmgBoost = 5;

        zone.Tick(OneMinute);

        Assert.Equal(5, state.DmgBoost);
    }

    [Fact]
    public void AnimalDoubleExp_FrozenWhenNoMountActive()
    {
        var (zone, state) = EnterPlayer(WarZone38);
        state.AnimalDoubleExp = 5;

        zone.Tick(OneMinute);

        Assert.Equal(5, state.AnimalDoubleExp);
    }

    [Fact]
    public void AnimalDoubleExp_DecrementsWhenMountedBelowExpCap()
    {
        var (zone, state) = EnterPlayer(WarZone38);
        state.AnimalDoubleExp = 5;
        state.AnimalIndex = 10;

        zone.Tick(OneMinute);

        Assert.Equal(4, state.AnimalDoubleExp);
    }


    [Fact]
    public void Zone101_CountsDownThenEvictsOneMinuteAfterReachingZero()
    {
        var (zone, state) = EnterPlayer(101);
        state.Level2 = 1;
        state.Zone101Time = 2;

        zone.Tick(OneMinute);
        Assert.Equal(1, state.Zone101Time);
        Assert.False(state.PaidZoneEvictionPending);

        zone.Tick(OneMinute);
        Assert.Equal(0, state.Zone101Time);
        Assert.False(state.PaidZoneEvictionPending);

        zone.Tick(OneMinute);
        Assert.True(state.PaidZoneEvictionPending);
    }

    [Fact]
    public void Zone101_AlreadyZero_FlagsEvictionAtFirstMinute()
    {
        var (zone, state) = EnterPlayer(101);
        state.Level2 = 1;
        state.Zone101Time = 0;

        zone.Tick(OneMinute);

        Assert.True(state.PaidZoneEvictionPending);
    }

    [Fact]
    public void Zone101_BelowProgressionThreshold_IsWhollySkipped()
    {
        var (zone, state) = EnterPlayer(101);
        state.Level2 = 0;
        state.Zone101Time = 0;

        zone.Tick(OneMinute);

        Assert.False(state.PaidZoneEvictionPending);
        Assert.Equal(0, state.Zone101Time);
    }

    [Fact]
    public void Zone101_PrivilegedUser_IsExemptFromEviction()
    {
        var (zone, state) = EnterPlayer(101);
        state.Level2 = 1;
        state.Zone101Time = 0;
        state.UserSort = 1;

        zone.Tick(OneMinute);

        Assert.False(state.PaidZoneEvictionPending);
    }

    [Fact]
    public void Zone101_MidZoneTransfer_IsSkipped()
    {
        var (zone, state) = EnterPlayer(101);
        state.Level2 = 1;
        state.Zone101Time = 0;
        state.IsMovingZone = true;

        zone.Tick(OneMinute);

        Assert.False(state.PaidZoneEvictionPending);
    }


    [Fact]
    public void Zone125_CountsDownThenEvicts()
    {
        var (zone, state) = EnterPlayer(125);
        state.TaiyanKeyTimer = 1;

        zone.Tick(OneMinute);
        Assert.Equal(0, state.TaiyanKeyTimer);
        Assert.False(state.PaidZoneEvictionPending);

        zone.Tick(OneMinute);
        Assert.True(state.PaidZoneEvictionPending);
    }


    [Fact]
    public void Zone126_ActivePremium_SuspendsCountdownAndEviction()
    {
        var (zone, state) = EnterPlayer(126);
        state.Zone126Time = 0;
        state.PremiumExpireUtc = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds();

        zone.Tick(OneMinute);

        Assert.False(state.PaidZoneEvictionPending);
        Assert.Equal(0, state.Zone126Time);
    }

    [Fact]
    public void Zone126_NoActivePremium_FlagsEviction()
    {
        var (zone, state) = EnterPlayer(126);
        state.Zone126Time = 0;
        state.PremiumExpireUtc = 0;

        zone.Tick(OneMinute);

        Assert.True(state.PaidZoneEvictionPending);
    }


    [Fact]
    public void Zone52_AlreadyZero_FlagsEviction()
    {
        var (zone, state) = EnterPlayer(52);
        state.Zone050Time2 = 0;

        zone.Tick(OneMinute);

        Assert.True(state.PaidZoneEvictionPending);
    }

    [Fact]
    public void NonPaidZone_NeverFlagsEviction()
    {
        var (zone, state) = EnterPlayer(PlainZone);

        zone.Tick(OneMinute);

        Assert.False(state.PaidZoneEvictionPending);
    }
}
