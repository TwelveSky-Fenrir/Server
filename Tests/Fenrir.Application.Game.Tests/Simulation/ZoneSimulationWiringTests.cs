using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="Zone" />'s ordered <see cref="ISimulationSystem" /> list: whole-legacy-tick gating, declared
///     order, and per-system fault containment.
/// </summary>
public class ZoneSimulationWiringTests
{
    [Fact]
    public void Tick_LessThanOneLegacyTickElapsed_NeverInvokesSimulationSystems()
    {
        var log = new List<string>();
        var system = new RecordingSystem(log, "only");
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system]);

        zone.Tick(TimeSpan.FromMilliseconds(200));

        Assert.Equal(0, system.CallCount);
    }

    [Fact]
    public void Tick_OneWholeLegacyTickElapsed_InvokesEverySystemOnceWithCountOne()
    {
        var log = new List<string>();
        var system = new RecordingSystem(log, "only");
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system]);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(1, system.CallCount);
        Assert.Equal(1, system.LastLegacyTicksElapsed);
    }

    [Fact]
    public void Tick_SeveralLegacyTicksDueAtOnce_PassesTheWholeBurstToEverySystem()
    {
        var log = new List<string>();
        var system = new RecordingSystem(log, "only");
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system]);

        // A stalled host: 3 whole legacy ticks (1.5 s) arrive in a single frame.
        zone.Tick(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(1, system.CallCount);
        Assert.Equal(3, system.LastLegacyTicksElapsed);
    }

    [Fact]
    public void Tick_MultipleSystems_RunInDeclaredOrder()
    {
        var log = new List<string>();
        var first = new RecordingSystem(log, "first");
        var second = new RecordingSystem(log, "second");
        var third = new RecordingSystem(log, "third");
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [first, second, third]);

        zone.Tick(SimulationClock.LegacyTick);

        string[] expectedOrder = ["first", "second", "third"];
        Assert.Equal(expectedOrder, log);
    }

    [Fact]
    public void Tick_OneSystemThrows_DoesNotPreventLaterSystemsFromRunning()
    {
        var log = new List<string>();
        var faulty = new RecordingSystem(log, "faulty", new InvalidOperationException("boom"));
        var healthy = new RecordingSystem(log, "healthy");
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [faulty, healthy]);

        zone.Tick(SimulationClock.LegacyTick);

        string[] expectedOrder = ["faulty", "healthy"];
        Assert.Equal(expectedOrder, log);
        Assert.Equal(1, healthy.CallCount);
    }

    [Fact]
    public void Tick_AccumulatesAcrossFrames_UntilAWholeLegacyTickIsDue()
    {
        var log = new List<string>();
        var system = new RecordingSystem(log, "only");
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [system]);

        // 9 frames of 50 ms (450 ms total) must not be enough for a single legacy tick yet.
        for (var i = 0; i < 9; i++)
            zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, system.CallCount);

        // The 10th frame crosses the 500 ms boundary.
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, system.CallCount);
        Assert.Equal(1, system.LastLegacyTicksElapsed);
    }

    private sealed class RecordingSystem(List<string> log, string name, Exception? throwOnSimulate = null)
        : ISimulationSystem
    {
        public int LastLegacyTicksElapsed { get; private set; }
        public int CallCount { get; private set; }

        public void Simulate(Zone zone, int legacyTicksElapsed)
        {
            log.Add(name);
            CallCount++;
            LastLegacyTicksElapsed = legacyTicksElapsed;

            if (throwOnSimulate is not null)
                throw throwOnSimulate;
        }
    }
}
