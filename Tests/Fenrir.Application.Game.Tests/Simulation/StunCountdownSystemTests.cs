using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="StunCountdownSystem" />: the ~1 s (2 legacy tick) stun-duration countdown and its
///     natural-expiry hand-off to <c>Zone.ClearStun</c> (<c>S07_MyGame04.cpp:438-459</c>).
/// </summary>
public class StunCountdownSystemTests
{
    private static (Zone Zone, PlayerRuntimeState State) EnterStunnedPlayer(int durationSeconds)
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new StunCountdownSystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.IsStunned = true;
        state.StunDurationSeconds = durationSeconds;
        state.CanUseConsumables = false;
        state.RepeatedStunCount = 4;

        return (zone, state);
    }

    [Fact]
    public void NotStunned_IsNeverTouched()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new StunCountdownSystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        zone.Tick(SimulationClock.ToTimeSpan(SimulationClock.StunCountdownLegacyTicks));

        Assert.False(state!.IsStunned);
        Assert.Equal(0, state.StunDurationSeconds);
    }

    [Fact]
    public void OneLegacyTickElapsed_IsTooEarly_NoDecrementYet()
    {
        var (zone, state) = EnterStunnedPlayer(5);

        zone.Tick(SimulationClock.LegacyTick); // only 1 of the required 2 legacy ticks

        Assert.True(state.IsStunned);
        Assert.Equal(5, state.StunDurationSeconds);
    }

    [Fact]
    public void TwoLegacyTicksElapsed_DecrementsDurationByOne()
    {
        var (zone, state) = EnterStunnedPlayer(5);

        zone.Tick(SimulationClock.ToTimeSpan(SimulationClock.StunCountdownLegacyTicks));

        Assert.True(state.IsStunned);
        Assert.Equal(4, state.StunDurationSeconds);
    }

    [Fact]
    public void BurstOfManyLegacyTicks_CatchesUpTheWholeAmountInOneCall()
    {
        var (zone, state) = EnterStunnedPlayer(20);

        // 6 whole "1-second" units (12 legacy ticks) arrive in a single stalled-host frame.
        zone.Tick(SimulationClock.ToTimeSpan(12));

        Assert.True(state.IsStunned);
        Assert.Equal(14, state.StunDurationSeconds);
    }

    [Fact]
    public void DurationReachingZero_ClearsStunAndResetsRepeatedStunCounter()
    {
        var (zone, state) = EnterStunnedPlayer(1);

        zone.Tick(SimulationClock.ToTimeSpan(SimulationClock.StunCountdownLegacyTicks));

        Assert.False(state.IsStunned);
        Assert.Equal(0, state.StunDurationSeconds);
        Assert.True(state.CanUseConsumables);
        Assert.Equal(0, state.RepeatedStunCount);
    }
}
