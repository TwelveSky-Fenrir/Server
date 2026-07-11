using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="TimedBuffCountdownSystem" />: the once-per-real-minute group-A (non-war) / group-B
///     (war/RvR) timed-buff countdowns, their server-partition gate, and the paid-zone occupancy countdown +
///     "evict one minute after the counter reaches zero" flag (Server/ts25zone/S07_MyGame04.cpp:913-1133).
/// </summary>
public class TimedBuffCountdownSystemTests
{
    // 120 legacy ticks == one real minute, the system's own cadence boundary.
    private static readonly TimeSpan OneMinute =
        SimulationClock.ToTimeSpan(SimulationClock.PlayTimeAccrualLegacyTicks);

    // A plain non-war map: group A runs, group B does not (not in either partition set).
    private const short PlainZone = 100;

    // Map 38: excluded from group A yet included in group B -- the contract's own A/B-inversion worked example.
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

    // --- Group A -----------------------------------------------------------------------------------------

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
        // Server 38 is excluded from group A: the drop/double-exp timers must not tick there.
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

        // 60 legacy ticks (30 s) -- half a minute, below the once-per-minute boundary.
        zone.Tick(SimulationClock.ToTimeSpan(60));

        Assert.Equal(5, state.DropItemTime);
    }

    [Fact]
    public void StalledHost_CatchesUpTheWholeMinutesElapsedInOnePass()
    {
        var (zone, state) = EnterPlayer(PlainZone);
        state.DropItemTime = 5;

        // 3 whole minutes (360 legacy ticks) arrive in a single stalled-host frame.
        zone.Tick(SimulationClock.ToTimeSpan(3 * SimulationClock.PlayTimeAccrualLegacyTicks));

        Assert.Equal(2, state.DropItemTime);
    }

    // --- Group B -----------------------------------------------------------------------------------------

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
        // AnimalIndex defaults to -1 (no mount) -- the gate must freeze the countdown.

        zone.Tick(OneMinute);

        Assert.Equal(5, state.AnimalDoubleExp);
    }

    [Fact]
    public void AnimalDoubleExp_DecrementsWhenMountedBelowExpCap()
    {
        var (zone, state) = EnterPlayer(WarZone38);
        state.AnimalDoubleExp = 5;
        state.AnimalIndex = 10; // actively mounted, garage slot 0 (accumulated exp 0 < MAX_MOUNT_EXP)

        zone.Tick(OneMinute);

        Assert.Equal(4, state.AnimalDoubleExp);
    }

    // --- Paid zone 101 -----------------------------------------------------------------------------------

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

        // The counter is already 0 this minute -> eviction is flagged now.
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
        state.Level2 = 0; // below the zone-101 gate
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
        state.UserSort = 1; // uUserSort >= 1 -> the whole paid-zone block is skipped

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

    // --- Paid zone 125 (aZone125Time / TaiyanKeyTimer) ---------------------------------------------------

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

    // --- Paid zone 126 (premium suspension) --------------------------------------------------------------

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
        state.PremiumExpireUtc = 0; // no premium

        zone.Tick(OneMinute);

        Assert.True(state.PaidZoneEvictionPending);
    }

    // --- Paid zone 52 (aZone050Time2) --------------------------------------------------------------------

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
