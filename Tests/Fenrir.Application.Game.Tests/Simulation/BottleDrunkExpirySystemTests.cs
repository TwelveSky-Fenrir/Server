using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

public class BottleDrunkExpirySystemTests
{
    private const short PlainZone = 100;

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) EnterPlayer()
    {
        var zone = ZoneTestKit.CreateZone(PlainZone, simulationSystems: [new BottleDrunkExpirySystem()]);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, PlainZone)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public void ActiveEffect_ExpiresOnceTheFullDurationElapses_AndNotifiesTheClient()
    {
        var (zone, state, pipe) = EnterPlayer();
        state.DrunkBottleTicksRemaining = BottleResolver.DrunkDurationTicks;
        state.DrunkBottleIndex = 2;

        zone.Tick(SimulationClock.ToTimeSpan(BottleResolver.DrunkDurationTicks));

        Assert.Equal(0, state.DrunkBottleTicksRemaining);
        Assert.Equal(2, state.DrunkBottleIndex);
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void ActiveEffect_WithTimeRemaining_IsOnlyDecremented_AndSendsNothing()
    {
        var (zone, state, pipe) = EnterPlayer();
        state.DrunkBottleTicksRemaining = BottleResolver.DrunkDurationTicks;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.True(state.DrunkBottleTicksRemaining is > 0 and < BottleResolver.DrunkDurationTicks);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NoActiveEffect_IsUntouched()
    {
        var (zone, state, pipe) = EnterPlayer();
        state.DrunkBottleTicksRemaining = 0;

        zone.Tick(SimulationClock.ToTimeSpan(BottleResolver.DrunkDurationTicks));

        Assert.Equal(0, state.DrunkBottleTicksRemaining);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
