using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="BuffExpirySystem" />: buff durations count down one per legacy tick, and a slot reaching
///     zero is fully cleared.
/// </summary>
public class BuffExpirySystemTests
{
    [Fact]
    public void Simulate_DecrementsDurationByOnePerLegacyTick()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[0] = 25; // slot 0 value
        state.Buffs.Buff[1] = 3; // slot 0 duration = 3 legacy ticks

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(25, state.Buffs.Buff[0]);
        Assert.Equal(2, state.Buffs.Buff[1]);
    }

    [Fact]
    public void Simulate_DurationReachingZero_ClearsBothValueAndDuration()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[10 * 2] = 40; // Critical buff slot
        state.Buffs.Buff[10 * 2 + 1] = 1; // 1 legacy tick left

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(0, state.Buffs.Buff[10 * 2]);
        Assert.Equal(0, state.Buffs.Buff[10 * 2 + 1]);
    }

    [Fact]
    public void Simulate_UnoccupiedSlot_IsNeverTouched()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.All(state!.Buffs.Buff, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Simulate_BurstOfMultipleLegacyTicks_DecrementsByTheWholeAmount()
    {
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [new BuffExpirySystem()]);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.Buffs.Buff[0] = 10;
        state.Buffs.Buff[1] = 10;

        // 3 whole legacy ticks (1.5s) arrive in a single stalled-host frame.
        zone.Tick(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(7, state.Buffs.Buff[1]);
    }
}
